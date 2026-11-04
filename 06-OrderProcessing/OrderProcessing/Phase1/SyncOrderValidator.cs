using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase1;

public sealed class SyncOrderValidator
{
    private readonly SyncCustomerRepository _customerRepository;
    private readonly SyncProductRepository _productRepository;
    private readonly SyncCouponRepository _couponRepository;

    public SyncOrderValidator(
        SyncCustomerRepository customerRepository,
        SyncProductRepository productRepository,
        SyncCouponRepository couponRepository)
    {
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _couponRepository = couponRepository;
    }

    public Order ValidateOrder(Order order)
    {
        order.FailureReason = null;

        var customer = _customerRepository.GetById(order.CustomerId);

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
            var product = _productRepository.GetById(item.ProductId);

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
            var coupon = _couponRepository.GetByCode(order.CouponCode);

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

        return order;
    }

    private static Order Reject(Order order, string reason)
    {
        order.Status = OrderStatus.Rejected;
        order.FailureReason = reason;

        return order;
    }
}