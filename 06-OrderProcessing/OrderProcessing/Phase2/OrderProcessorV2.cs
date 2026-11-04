using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public sealed class OrderProcessorV2
{
    private readonly ProductRepository _productRepository;
    private readonly OrderPricer _orderPricer;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly OrderRepository _orderRepository;

    public OrderProcessorV2(
        ProductRepository productRepository,
        OrderPricer orderPricer,
        InvoiceGenerator invoiceGenerator,
        OrderRepository orderRepository)
    {
        _productRepository = productRepository;
        _orderPricer = orderPricer;
        _invoiceGenerator = invoiceGenerator;
        _orderRepository = orderRepository;
    }

    public async Task<bool> ProcessValidatedOrderAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info($"[OrderProcessorV2] Processing order #{order.Id}");

        var reservedItems = new List<OrderItem>();

        foreach (var item in order.Items)
        {
            bool reserved = await _productRepository.ReserveStockAsync(
                item.ProductId,
                item.Quantity,
                ct);

            if (!reserved)
            {
                foreach (var reservedItem in reservedItems)
                {
                    await _productRepository.ReleaseStockAsync(
                        reservedItem.ProductId,
                        reservedItem.Quantity,
                        ct);
                }

                Product? product = await _productRepository.GetByIdAsync(
                    item.ProductId,
                    ct);

                order.Status = OrderStatus.Rejected;
                order.FailureReason =
                    $"Insufficient stock for '{product?.Name ?? $"Product #{item.ProductId}"}'";

                await _orderRepository.SaveAsync(order, ct);

                Log.Error(
                    $"[OrderProcessorV2] Order #{order.Id} rejected: {order.FailureReason}");

                return false;
            }

            reservedItems.Add(item);
        }

        await _orderPricer.PriceOrderAsync(order, ct);

        await _invoiceGenerator.GenerateAsync(order, ct);

        order.Status = OrderStatus.Completed;
        order.FailureReason = null;

        await _orderRepository.SaveAsync(order, ct);

        Log.Success($"[OrderProcessorV2] Order #{order.Id} completed");

        return true;
    }
}