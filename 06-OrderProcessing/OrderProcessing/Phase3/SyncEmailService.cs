using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class SyncEmailService
{
    private readonly JsonFileStore _store;

    public SyncEmailService(JsonFileStore store)
    {
        _store = store;
    }

    public void SendConfirmation(Order order, Customer customer)
    {
        Thread.Sleep(200);
        Thread.Sleep(300);
        Thread.Sleep(500);

        _store.Update<EmailMessage>(
            DataFiles.Emails,
            emails =>
            {
                emails.Add(new EmailMessage
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    To = customer.Email,
                    Subject = $"Order #{order.Id} confirmation",
                    Body = $"Your order #{order.Id} has been completed. Total: {order.TotalAmount:F2}",
                    SentAt = DateTime.Now,
                    Attempts = 1,
                    LastError = null
                });

                return emails;
            });
    }
}