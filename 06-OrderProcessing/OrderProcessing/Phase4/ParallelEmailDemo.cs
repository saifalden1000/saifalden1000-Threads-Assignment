using System.Collections.Concurrent;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class ParallelEmailDemo
{
    private readonly JsonFileStore _store;

    public ParallelEmailDemo(JsonFileStore store)
    {
        _store = store;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info("========== PARALLEL EMAIL PROCESSING ==========");

        var customers = await _store.ReadAsync<Customer>(
            DataFiles.Customers,
            ct);

        var customer = customers.First(c => c.Id == 1);

        var testOrderIds = Enumerable.Range(1001, 30).ToHashSet();

        await _store.UpdateAsync<EmailMessage>(
            DataFiles.Emails,
            emails => emails
                .Where(email => !testOrderIds.Contains(email.OrderId))
                .ToList(),
            ct);

        var emailService = new EmailService(
            _store,
            new Dictionary<int, int>());

        var completedOrders = new ConcurrentDictionary<int, byte>();

        int activeEmails = 0;
        int maximumActiveEmails = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(1001, 30),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = ct
            },
            async (orderId, token) =>
            {
                int currentActive = Interlocked.Increment(
                    ref activeEmails);

                int previousMaximum;

                do
                {
                    previousMaximum = Volatile.Read(
                        ref maximumActiveEmails);

                    if (currentActive <= previousMaximum)
                        break;

                } while (Interlocked.CompareExchange(
                    ref maximumActiveEmails,
                    currentActive,
                    previousMaximum) != previousMaximum);

                try
                {
                    var order = new Order
                    {
                        Id = orderId,
                        CustomerId = customer.Id,
                        Items = [],
                        SubTotal = 100m,
                        Discount = 0,
                        ShippingCost = 0,
                        TotalAmount = 100m,
                        Status = OrderStatus.Completed,
                        CreatedAt = DateTime.Now,
                        FailureReason = null,
                        TransactionId = null
                    };

                    await emailService.SendConfirmationAsync(
                        order,
                        customer,
                        token);

                    completedOrders.TryAdd(orderId, 0);
                }
                finally
                {
                    Interlocked.Decrement(ref activeEmails);
                }
            });

        var savedEmails = await _store.ReadAsync<EmailMessage>(
            DataFiles.Emails,
            ct);

        int testEmailsSaved = savedEmails.Count(
            email => testOrderIds.Contains(email.OrderId));

        int uniqueEmails = savedEmails
            .Where(email => testOrderIds.Contains(email.OrderId))
            .Select(email => email.OrderId)
            .Distinct()
            .Count();

        Log.Success($"Successfully completed: {completedOrders.Count}/30");
        Log.Success($"Emails saved: {testEmailsSaved}/30");
        Log.Info($"Unique email recipients: {uniqueEmails}");
        Log.Info($"Maximum concurrent emails: {maximumActiveEmails}");
    }
}