using OrderProcessing.Shared.Models;

namespace OrderProcessing.Shared.Models;

public sealed class CustomerDashboard
{
    public Customer Customer { get; set; } = new();

    public List<Order> Orders { get; set; } = [];

    public List<PaymentTransaction> Payments { get; set; } = [];

    public List<EmailMessage> Emails { get; set; } = [];
}