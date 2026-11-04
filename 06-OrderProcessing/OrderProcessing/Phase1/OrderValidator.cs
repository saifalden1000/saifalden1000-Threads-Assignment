using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public sealed class OrderValidator
{
    private readonly CustomerRepository _customerRepository;
    private readonly ProductRepository _productRepository;
    private readonly CouponRepository _couponRepository;

    public OrderValidator(
        CustomerRepository customerRepository,
        ProductRepository productRepository,
        CouponRepository couponRepository)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _couponRepository = couponRepository;
    }

    public async Task<Order> ValidateOrderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        Log.Info($"[OrderValidator] Validating order #{order.Id}");

        order.FailureReason = null;

        var customer = await _customerRepository.GetByIdAsync(
            order.CustomerId,
            cancellationToken);

        if (customer is null)
        {
            return Reject(order, $"Customer #{order.CustomerId} not found");
        }

        if (!customer.IsActive)
        {
            return Reject(order, "Customer is inactive");
        }

        foreach (var item in order.Items)
        {
            var product = await _productRepository.GetByIdAsync(
                item.ProductId,
                cancellationToken);

            if (product is null)
            {
                return Reject(order, $"Product #{item.ProductId} not found");
            }

            if (product.StockQuantity < item.Quantity)
            {
                return Reject(
                    order,
                    $"Insufficient stock for '{product.Name}'");
            }
        }

        if (order.CouponCode is not null)
        {
            var coupon = await _couponRepository.GetByCodeAsync(
                order.CouponCode,
                cancellationToken);

            if (coupon is null)
            {
                return Reject(
                    order,
                    $"Coupon '{order.CouponCode}' does not exist");
            }

            if (coupon.ExpiryDate.Date < DateTime.Today)
            {
                return Reject(
                    order,
                    $"Coupon '{order.CouponCode}' has expired");
            }
        }

        order.Status = OrderStatus.Validated;
        order.FailureReason = null;

        Log.Success($"[OrderValidator] Order #{order.Id} validated");

        return order;
    }

    private static Order Reject(Order order, string reason)
    {
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;

        Log.Error($"[OrderValidator] Order #{order.Id} rejected: {reason}");

        return order;
    }
}