using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public sealed class OrderProcessor
{
    private readonly CustomerService _customerService;
    private readonly InventoryService _inventoryService;
    private readonly PricingService _pricingService;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IEmailService _emailService;
    private readonly OrderRepository _orderRepository;
    private readonly ProductRepository _productRepository;

    public OrderProcessor(
        CustomerService customerService,
        InventoryService inventoryService,
        PricingService pricingService,
        IPaymentGateway paymentGateway,
        IEmailService emailService,
        OrderRepository orderRepository,
        ProductRepository productRepository)
    {
        _customerService = customerService;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _emailService = emailService;
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async Task<bool> ProcessOrderAsync(
        Order order,
        CancellationToken ct)
    {
        bool inventoryReserved = false;
        string? transactionId = null;
        Customer? customer = null;

        Log.Info($"Order #{order.Id} -> Starting checkout");

        try
        {
            var stopwatch = Stopwatch.StartNew();

            customer = await _customerService.GetCustomerAsync(
                order.CustomerId,
                ct);

            LogStep(order.Id, "1/6 Customer", stopwatch, "OK");

            if (!customer.IsActive)
            {
                return await RejectOrderAsync(
                    order,
                    "Customer is inactive",
                    ct);
            }

            stopwatch.Restart();

            bool reserved = await _inventoryService.ReserveStockAsync(
                order,
                ct);

            LogStep(
                order.Id,
                "2/6 Inventory",
                stopwatch,
                reserved ? "OK" : "INSUFFICIENT STOCK");

            if (!reserved)
            {
                string reason = await GetInsufficientStockReasonAsync(
                    order,
                    ct);

                return await RejectOrderAsync(order, reason, ct);
            }

            inventoryReserved = true;

            stopwatch.Restart();

            await _pricingService.CalculateTotalAsync(order, ct);

            LogStep(order.Id, "3/6 Pricing", stopwatch, "OK");

            stopwatch.Restart();

            using var paymentCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct);

            Task<PaymentResult> paymentTask =
                _paymentGateway.ProcessPaymentAsync(
                    order.Id,
                    order.TotalAmount,
                    paymentCts.Token);

            Task timeoutTask = Task.Delay(
                TimeSpan.FromSeconds(3),
                ct);

            Task completedTask = await Task.WhenAny(
                paymentTask,
                timeoutTask);

            if (ct.IsCancellationRequested)
            {
                paymentCts.Cancel();

                try
                {
                    PaymentResult completedPayment = await paymentTask;

                    if (completedPayment.Success)
                    {
                        transactionId = completedPayment.TransactionId;
                        order.TransactionId = transactionId;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Log.Error(
                        $"Order #{order.Id} payment stopped during cancellation: {ex.Message}");
                }

                throw new OperationCanceledException(ct);
            }

            if (completedTask != paymentTask)
            {
                paymentCts.Cancel();

                try
                {
                    PaymentResult latePayment = await paymentTask;

                    if (latePayment.Success)
                    {
                        transactionId = latePayment.TransactionId;
                        order.TransactionId = transactionId;

                        await _paymentGateway.RefundAsync(
                            transactionId,
                            CancellationToken.None);

                        transactionId = null;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Log.Error(
                        $"Order #{order.Id} payment ended after timeout: {ex.Message}");
                }

                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                inventoryReserved = false;

                LogStep(order.Id, "4/6 Payment", stopwatch, "TIMEOUT");

                return await PaymentFailedAsync(
                    order,
                    "Payment gateway timeout (3s)",
                    ct);
            }

            PaymentResult paymentResult = await paymentTask;

            LogStep(
                order.Id,
                "4/6 Payment",
                stopwatch,
                paymentResult.Success ? "OK" : "DECLINED");

            if (!paymentResult.Success)
            {
                await _inventoryService.ReleaseStockAsync(
                    order,
                    ct);

                inventoryReserved = false;

                return await PaymentFailedAsync(
                    order,
                    paymentResult.Message ?? "Card declined by issuer",
                    ct);
            }

            transactionId = paymentResult.TransactionId;
            order.TransactionId = transactionId;

            stopwatch.Restart();

            order.Status = OrderStatus.Completed;
            order.FailureReason = null;

            try
            {
                await _orderRepository.SaveAsync(order, ct);
            }
            catch (DatabaseWriteException)
            {
                if (transactionId is not null)
                {
                    await _paymentGateway.RefundAsync(
                        transactionId,
                        CancellationToken.None);
                }

                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                inventoryReserved = false;

                order.Status = OrderStatus.Failed;
                order.FailureReason =
                    $"Database save failed after payment; payment {transactionId} refunded";

                await _orderRepository.SaveAsync(
                    order,
                    CancellationToken.None);

                LogStep(order.Id, "5/6 Save", stopwatch, "REFUNDED");

                return false;
            }

            LogStep(order.Id, "5/6 Save", stopwatch, "OK");

            stopwatch.Restart();

            await _emailService.SendConfirmationWithRetryAsync(
                order,
                customer,
                ct);

            LogStep(order.Id, "6/6 Email", stopwatch, "OK");

            Log.Success($"Order #{order.Id} -> Checkout completed");

            return order.Status == OrderStatus.Completed;
        }
        catch (CustomerNotFoundException ex)
        {
            return await RejectOrderAsync(order, ex.Message, ct);
        }
        catch (InvalidCouponException ex)
        {
            if (inventoryReserved)
            {
                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                inventoryReserved = false;
            }

            return await RejectOrderAsync(order, ex.Message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await CompensateCancellationAsync(
                order,
                transactionId,
                inventoryReserved);

            return false;
        }
    }

    private async Task CompensateCancellationAsync(
        Order order,
        string? transactionId,
        bool inventoryReserved)
    {
        if (transactionId is not null)
        {
            try
            {
                await _paymentGateway.RefundAsync(
                    transactionId,
                    CancellationToken.None);

                Log.Info(
                    $"Order #{order.Id}: payment {transactionId} refunded after cancellation");
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Order #{order.Id}: refund failed during cancellation: {ex.Message}");
            }
        }

        if (inventoryReserved)
        {
            try
            {
                await _inventoryService.ReleaseStockAsync(
                    order,
                    CancellationToken.None);

                Log.Info(
                    $"Order #{order.Id}: inventory released after cancellation");
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Order #{order.Id}: inventory release failed during cancellation: {ex.Message}");
            }
        }

        order.Status = OrderStatus.Pending;
        order.FailureReason = "Cancelled";

        await _orderRepository.SaveAsync(
            order,
            CancellationToken.None);

        Log.Error($"Order #{order.Id} -> Cancelled and compensated");
    }

    private async Task<bool> RejectOrderAsync(
        Order order,
        string reason,
        CancellationToken ct)
    {
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;

        try
        {
            await _orderRepository.SaveAsync(order, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            order.Status = OrderStatus.Pending;
            order.FailureReason = "Cancelled";

            await _orderRepository.SaveAsync(
                order,
                CancellationToken.None);

            return false;
        }

        Log.Error($"Order #{order.Id} -> Rejected: {reason}");

        return false;
    }

    private async Task<bool> PaymentFailedAsync(
        Order order,
        string reason,
        CancellationToken ct)
    {
        order.Status = OrderStatus.PaymentFailed;
        order.FailureReason = reason;

        try
        {
            await _orderRepository.SaveAsync(order, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            order.Status = OrderStatus.Pending;
            order.FailureReason = "Cancelled";

            await _orderRepository.SaveAsync(
                order,
                CancellationToken.None);

            return false;
        }

        Log.Error($"Order #{order.Id} -> Payment failed: {reason}");

        return false;
    }

    private async Task<string> GetInsufficientStockReasonAsync(
        Order order,
        CancellationToken ct)
    {
        foreach (var item in order.Items)
        {
            Product? product = await _productRepository.GetByIdAsync(
                item.ProductId,
                ct);

            if (product is null)
                return $"Product #{item.ProductId} not found";

            if (product.StockQuantity < item.Quantity)
                return $"Insufficient stock for '{product.Name}'";
        }

        return "Insufficient stock";
    }

    private static void LogStep(
        int orderId,
        string step,
        Stopwatch stopwatch,
        string result)
    {
        stopwatch.Stop();

        Log.Info(
            $"Order #{orderId} -> Step {step} {result} " +
            $"in {stopwatch.ElapsedMilliseconds} ms");
    }
}