using System.Collections.Concurrent;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class EmailService : IEmailService
{
    private readonly JsonFileStore _store;
    private readonly IReadOnlyDictionary<int, int> _failAttempts;
    private readonly ConcurrentDictionary<int, int> _attemptsByOrder = new();

    public EmailService(
        JsonFileStore store,
        IReadOnlyDictionary<int, int> failAttempts)
    {
        _store = store;
        _failAttempts = failAttempts;
    }

    public async Task SendConfirmationAsync(
        Order order,
        Customer customer,
        CancellationToken ct)
    {
        int attempt = _attemptsByOrder.AddOrUpdate(
            order.Id,
            1,
            (_, current) => current + 1);

        Log.Info($"[EmailService] Order #{order.Id}, attempt {attempt}: rendering template");

        await Task.Delay(200, ct);

        Log.Info($"[EmailService] Order #{order.Id}: connecting to SMTP");

        await Task.Delay(300, ct);

        Log.Info($"[EmailService] Order #{order.Id}: sending email");

        await Task.Delay(500, ct);

        if (_failAttempts.TryGetValue(order.Id, out int failures)
            && attempt <= failures)
        {
            throw new SmtpUnavailableException(
                $"SMTP unavailable for Order #{order.Id}, attempt {attempt}");
        }

        var email = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} confirmation",
            Body = $"Your order #{order.Id} has been completed. Total: {order.TotalAmount:F2}",
            SentAt = DateTime.Now,
            Attempts = attempt,
            LastError = null
        };

        await _store.UpdateAsync<EmailMessage>(
            DataFiles.Emails,
            emails =>
            {
                emails.Add(email);
                return emails;
            },
            ct);

        Log.Success(
            $"[EmailService] Email sent for Order #{order.Id} on attempt {attempt}");
    }

    public async Task SendConfirmationWithRetryAsync(
        Order order,
        Customer customer,
        CancellationToken ct)
    {
        int[] retryDelays = { 500, 1000, 2000 };
        string lastError = string.Empty;

        for (int attempt = 1; attempt <= 4; attempt++)
        {
            try
            {
                await SendConfirmationAsync(order, customer, ct);
                return;
            }
            catch (SmtpUnavailableException ex)
            {
                lastError = ex.Message;

                Log.Error(
                    $"[EmailService] Order #{order.Id}: attempt {attempt} failed: {ex.Message}");

                if (attempt == 4)
                    break;

                await Task.Delay(retryDelays[attempt - 1], ct);
            }
        }

        var failedEmail = new EmailMessage
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            To = customer.Email,
            Subject = $"Order #{order.Id} confirmation",
            Body = $"Your order #{order.Id} has been completed.",
            SentAt = DateTime.Now,
            Attempts = 4,
            LastError = lastError
        };

        await _store.UpdateAsync<EmailMessage>(
            DataFiles.EmailRetry,
            emails =>
            {
                emails.Add(failedEmail);
                return emails;
            },
            ct);

        Log.Error(
            $"[EmailService] Email for Order #{order.Id} queued for retry later");
    }
}