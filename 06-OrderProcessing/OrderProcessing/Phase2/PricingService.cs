using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class PricingService
{
    private readonly JsonFileStore _store;
    private readonly CustomerRepository _customerRepository;
    private readonly CouponRepository _couponRepository;
    private readonly HashSet<int> _failShippingForOrderIds;

    public PricingService(
        JsonFileStore store,
        CustomerRepository customerRepository,
        CouponRepository couponRepository,
        IEnumerable<int> failShippingForOrderIds)
    {
        _store = store;
        _customerRepository = customerRepository;
        _couponRepository = couponRepository;
        _failShippingForOrderIds = failShippingForOrderIds.ToHashSet();
    }

    public async Task<decimal> CalculateSubTotalAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        Log.Info($"[PricingService] Opening product data connection for order #{order.Id}");

        await Task.Delay(200, cancellationToken);

        Log.Info($"[PricingService] Querying products for order #{order.Id}");

        await Task.Delay(Random.Shared.Next(300, 601), cancellationToken);

        var products = await _store.ReadAsync<Product>(
            DataFiles.Products,
            cancellationToken);

        decimal subtotal = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(
                product => product.Id == item.ProductId);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found");
            }

            item.UnitPrice = product.Price;

            subtotal += item.UnitPrice * item.Quantity;
        }

        order.SubTotal = Math.Round(
            subtotal,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success($"[PricingService] Order #{order.Id} subtotal: {order.SubTotal}");

        return order.SubTotal;
    }

    public async Task<decimal> GetDiscountAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        Log.Info($"[PricingService] Connecting to discount service for order #{order.Id}");

        await Task.Delay(150, cancellationToken);

        Log.Info($"[PricingService] Sending discount request for order #{order.Id}");

        await Task.Delay(300, cancellationToken);

        Log.Info($"[PricingService] Waiting for discount response for order #{order.Id}");

        await Task.Delay(Random.Shared.Next(500, 1501), cancellationToken);

        var customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            cancellationToken);

        if (customer is null)
        {
            throw new InvalidOperationException(
                $"Customer #{order.CustomerId} not found");
        }

        var discountPercent = customer.LoyaltyTier switch
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
                cancellationToken);

            if (coupon is null)
            {
                throw new InvalidOperationException(
                    $"Coupon '{order.CouponCode}' does not exist");
            }

            discountPercent += coupon.DiscountPercent;
        }

        discountPercent = Math.Min(discountPercent, 30);

        var discount = order.SubTotal * discountPercent / 100m;

        discount = Math.Round(
            discount,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"[PricingService] Order #{order.Id} discount: {discount}");

        return discount;
    }

    public async Task<decimal> CalculateShippingCostAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        Log.Info($"[PricingService] Connecting to shipping service for order #{order.Id}");

        await Task.Delay(150, cancellationToken);

        if (_failShippingForOrderIds.Contains(order.Id))
        {
            Log.Error(
                $"[PricingService] Shipping unavailable for order #{order.Id}");

            throw new ShippingProviderUnavailableException(
                "Shipping provider unavailable");
        }

        Log.Info($"[PricingService] Sending shipping request for order #{order.Id}");

        await Task.Delay(300, cancellationToken);

        Log.Info($"[PricingService] Waiting for shipping response for order #{order.Id}");

        await Task.Delay(Random.Shared.Next(500, 1501), cancellationToken);

        var products = await _store.ReadAsync<Product>(
            DataFiles.Products,
            cancellationToken);

        var totalQuantity = 0;
        decimal totalWeight = 0;

        foreach (var item in order.Items)
        {
            var product = products.FirstOrDefault(
                product => product.Id == item.ProductId);

            if (product is null)
            {
                throw new InvalidOperationException(
                    $"Product #{item.ProductId} not found");
            }

            totalQuantity += item.Quantity;
            totalWeight += product.WeightKg * item.Quantity;
        }

        var shippingCost = order.SubTotal >= 1000m
            ? 0m
            : 5m + (1.50m * totalQuantity) + (2.00m * totalWeight);

        shippingCost = Math.Round(
            shippingCost,
            2,
            MidpointRounding.AwayFromZero);

        Log.Success(
            $"[PricingService] Order #{order.Id} shipping: {shippingCost}");

        return shippingCost;
    }
}