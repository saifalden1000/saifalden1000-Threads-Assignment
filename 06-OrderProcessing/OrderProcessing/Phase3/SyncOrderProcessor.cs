using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class SyncOrderProcessor
{
    private readonly JsonFileStore _store;
    private readonly SyncCustomerService _customerService;
    private readonly SyncInventoryService _inventoryService;
    private readonly SyncPricingService _pricingService;
    private readonly SyncPaymentGateway _paymentGateway;
    private readonly SyncEmailService _emailService;
    private readonly OrderRepository _orderRepository;

    public SyncOrderProcessor(
        JsonFileStore store,
        SyncCustomerService customerService,
        SyncInventoryService inventoryService,
        SyncPricingService pricingService,
        SyncPaymentGateway paymentGateway,
        SyncEmailService emailService,
        OrderRepository orderRepository)
    {
        _store = store;
        _customerService = customerService;
        _inventoryService = inventoryService;
        _pricingService = pricingService;
        _paymentGateway = paymentGateway;
        _emailService = emailService;
        _orderRepository = orderRepository;
    }

    public bool ProcessOrder(Order order)
    {
        Log.Info($"[SyncOrderProcessor] Processing Order #{order.Id}");

        try
        {
            Customer customer = _customerService.GetCustomer(order.CustomerId);

            if (!customer.IsActive)
            {
                return Reject(order, "Customer is inactive");
            }

            if (!_inventoryService.ReserveStock(order))
            {
                return Reject(order, GetStockFailureReason(order));
            }

            try
            {
                _pricingService.CalculateTotal(order);
            }
            catch (InvalidCouponException ex)
            {
                _inventoryService.ReleaseStock(order);
                return Reject(order, ex.Message);
            }

            var payment = _paymentGateway.ProcessPayment(
                order.Id,
                order.TotalAmount);

            if (!payment.Success)
            {
                _inventoryService.ReleaseStock(order);
                order.Status = OrderStatus.PaymentFailed;
                order.FailureReason = payment.Message;
                _orderRepository.Save(order);
                return false;
            }

            order.TransactionId = payment.TransactionId;
            order.Status = OrderStatus.Completed;
            order.FailureReason = null;

            _orderRepository.Save(order);

            _emailService.SendConfirmation(order, customer);

            Log.Success(
                $"[SyncOrderProcessor] Order #{order.Id} completed");

            return true;
        }
        catch (CustomerNotFoundException ex)
        {
            return Reject(order, ex.Message);
        }
    }

    private bool Reject(Order order, string reason)
    {
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;

        _orderRepository.Save(order);

        Log.Error(
            $"[SyncOrderProcessor] Order #{order.Id} rejected: {reason}");

        return false;
    }

    private string GetStockFailureReason(Order order)
    {
        var products = _store.Read<Product>(DataFiles.Products);

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(
                p => p.Id == item.ProductId);

            if (product is null)
                return $"Product #{item.ProductId} not found";

            if (product.StockQuantity < item.Quantity)
                return $"Insufficient stock for '{product.Name}'";
        }

        return "Insufficient stock";
    }
}