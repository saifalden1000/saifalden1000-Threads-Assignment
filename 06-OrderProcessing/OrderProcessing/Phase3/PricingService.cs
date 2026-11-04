using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class PricingService
{
    private readonly JsonFileStore _store;
    private readonly CustomerRepository _customerRepository;
    private readonly CouponRepository _couponRepository;

    public PricingService(
        JsonFileStore store,
        CustomerRepository customerRepository,
        CouponRepository couponRepository)
    {
        _store = store;
        _customerRepository = customerRepository;
        _couponRepository = couponRepository;
    }

    public async Task<decimal> CalculateTotalAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info($"[PricingService] Calculating total for Order #{order.Id}");

        Log.Info("[PricingService] Opening product connection...");
        await Task.Delay(200, ct);

        Log.Info("[PricingService] Executing product query...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var products = await _store.ReadAsync<Product>(
            DataFiles.Products,
            ct);

        decimal subtotal = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(
                p => p.Id == item.ProductId);

            if (product is null)
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found");

            item.UnitPrice = product.Price;
            subtotal += product.Price * item.Quantity;
        }

        order.SubTotal = Math.Round(
            subtotal, 2, MidpointRounding.AwayFromZero);

        var discountTask = CalculateDiscountAsync(order, ct);
        var shippingTask = CalculateShippingAsync(order, ct);

        decimal discount;

        try
        {
            discount = await discountTask;
        }
        catch
        {
            try
            {
                await shippingTask;
            }
            catch
            {
            }

            throw;
        }

        decimal shipping = await shippingTask;

        order.Discount = discount;
        order.ShippingCost = shipping;

        order.TotalAmount = Math.Round(
            order.SubTotal - order.Discount + order.ShippingCost,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"[PricingService] Order #{order.Id} total: {order.TotalAmount}");

        return order.TotalAmount;
    }

    private async Task<decimal> CalculateDiscountAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info("[PricingService] Discount API: DNS/handshake...");
        await Task.Delay(150, ct);

        Log.Info("[PricingService] Discount API: Sending request...");
        await Task.Delay(300, ct);

        Log.Info("[PricingService] Discount API: Waiting for response...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        var customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            ct);

        if (customer is null)
            throw new InvalidOperationException(
                $"Customer #{order.CustomerId} not found");

        int discountPercent = customer.LoyaltyTier switch
        {
            LoyaltyTier.Bronze => 0,
            LoyaltyTier.Silver => 5,
            LoyaltyTier.Gold => 10,
            _ => 0
        };

        if (order.CouponCode is not null)
        {
            var coupon = await _couponRepository.GetByCodeAsync(
                order.CouponCode,
                ct);

            if (coupon is null)
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' does not exist");

            if (coupon.ExpiryDate.Date < DateTime.Today)
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' has expired");

            discountPercent += coupon.DiscountPercent;
        }

        discountPercent = Math.Min(discountPercent, 30);

        return Math.Round(
            order.SubTotal * discountPercent / 100m,
            2,
            MidpointRounding.AwayFromZero);
    }

    private async Task<decimal> CalculateShippingAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info("[PricingService] Shipping API: DNS/handshake...");
        await Task.Delay(150, ct);

        Log.Info("[PricingService] Shipping API: Sending request...");
        await Task.Delay(300, ct);

        Log.Info("[PricingService] Shipping API: Waiting for response...");
        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        var products = await _store.ReadAsync<Product>(
            DataFiles.Products,
            ct);

        int totalQuantity = 0;
        decimal totalWeight = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(
                p => p.Id == item.ProductId);

            if (product is null)
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found");

            totalQuantity += item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
        }

        decimal shipping = order.SubTotal >= 1000m
            ? 0m
            : 5m + (1.50m * totalQuantity) + (2m * totalWeight);

        return Math.Round(
            shipping,
            2,
            MidpointRounding.AwayFromZero);
    }
}