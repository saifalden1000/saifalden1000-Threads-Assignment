using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase2;

public sealed class OrderPricer
{
    private readonly PricingService _pricingService;

    public OrderPricer(PricingService pricingService)
    {
        _pricingService = pricingService;
    }

    public async Task PriceOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        Log.Info($"[OrderPricer] Pricing order #{order.Id}");

        await _pricingService.CalculateSubTotalAsync(
            order,
            cancellationToken);

        var discountTask = _pricingService.GetDiscountAsync(
            order,
            cancellationToken);

        var shippingTask = _pricingService.CalculateShippingCostAsync(
            order,
            cancellationToken);

        order.Discount = await discountTask;
        order.ShippingCost = await shippingTask;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"[OrderPricer] Order #{order.Id} total: {order.TotalAmount}");
    }

    public async Task PriceOrderSequentialAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        Log.Info($"[OrderPricer] Sequential pricing for order #{order.Id}");

        await _pricingService.CalculateSubTotalAsync(
            order,
            cancellationToken);

        order.Discount = await _pricingService.GetDiscountAsync(
            order,
            cancellationToken);

        order.ShippingCost = await _pricingService.CalculateShippingCostAsync(
            order,
            cancellationToken);

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"[OrderPricer] Order #{order.Id} total: {order.TotalAmount}");
    }
}