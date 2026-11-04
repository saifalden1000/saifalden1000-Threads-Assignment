using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class SyncPricingService
{
    private readonly JsonFileStore _store;
    private readonly SyncCustomerRepository _customerRepository;
    private readonly SyncProductRepository _productRepository;
    private readonly SyncCouponRepository _couponRepository;

    public SyncPricingService(
        JsonFileStore store,
        SyncCustomerRepository customerRepository,
        SyncProductRepository productRepository,
        SyncCouponRepository couponRepository)
    {
        _store = store;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _couponRepository = couponRepository;
    }

    public decimal CalculateTotal(Order order)
    {
        Thread.Sleep(200);
        Thread.Sleep(Random.Shared.Next(300, 601));

        var products = _store.Read<Product>(DataFiles.Products);

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

        order.SubTotal = RoundMoney(subtotal);

        Thread.Sleep(150);
        Thread.Sleep(300);
        Thread.Sleep(Random.Shared.Next(500, 1501));

        var customer = _customerRepository.GetById(order.CustomerId);

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
            Thread.Sleep(150);
            Thread.Sleep(300);
            Thread.Sleep(Random.Shared.Next(500, 1501));

            var coupon = _couponRepository.GetByCode(order.CouponCode);

            if (coupon is null)
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' does not exist");

            if (coupon.ExpiryDate.Date < DateTime.Today)
                throw new InvalidCouponException(
                    $"Coupon '{order.CouponCode}' has expired");

            discountPercent += coupon.DiscountPercent;
        }

        discountPercent = Math.Min(discountPercent, 30);

        order.Discount = RoundMoney(
            order.SubTotal * discountPercent / 100m);

        Thread.Sleep(150);
        Thread.Sleep(300);
        Thread.Sleep(Random.Shared.Next(500, 1501));

        products = _store.Read<Product>(DataFiles.Products);

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

        order.ShippingCost = order.SubTotal >= 1000m
            ? 0m
            : RoundMoney(5m + 1.50m * totalQuantity + 2m * totalWeight);

        order.TotalAmount = RoundMoney(
            order.SubTotal - order.Discount + order.ShippingCost);

        return order.TotalAmount;
    }

    private static decimal RoundMoney(decimal value)
    {
        return Math.Round(
            value,
            2,
            MidpointRounding.AwayFromZero);
    }
}