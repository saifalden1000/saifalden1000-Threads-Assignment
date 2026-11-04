using OrderProcessing.Phase1;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class CustomerDashboardService
{
    private readonly JsonFileStore _store;
    private readonly CustomerService _customerService;
    private readonly OrderRepository _orderRepository;
    private readonly HashSet<int> _failPaymentsForCustomerIds;

    public CustomerDashboardService(
        JsonFileStore store,
        IEnumerable<int>? failPaymentsForCustomerIds = null)
    {
        _store = store;
        _customerService = new CustomerService(store);
        _orderRepository = new OrderRepository(store);

        _failPaymentsForCustomerIds =
            (failPaymentsForCustomerIds ?? []).ToHashSet();
    }

    public async Task<CustomerDashboard> LoadAsync(
        int customerId,
        CancellationToken ct)
    {
        var dashboard = new CustomerDashboard();

        Log.Info($"[Dashboard] Starting four loads for Customer #{customerId}");

        Task<Action> profileTask = LoadAndAssignAsync(
            () => _customerService.GetCustomerAsync(customerId, ct),
            customer => dashboard.Customer = customer);

        Task<Action> ordersTask = LoadAndAssignAsync(
            async () =>
            {
                var orders = await _orderRepository.GetAllAsync(ct);

                return orders
                    .Where(o => o.CustomerId == customerId)
                    .ToList();
            },
            orders => dashboard.Orders = orders);

        Task<Action> paymentsTask = LoadAndAssignAsync(
            () => LoadPaymentsAsync(customerId, ct),
            payments => dashboard.Payments = payments);

        Task<Action> emailsTask = LoadAndAssignAsync(
            () => LoadEmailsAsync(customerId, ct),
            emails => dashboard.Emails = emails);

        Task<Action>[] loads =
        [
            profileTask,
            ordersTask,
            paymentsTask,
            emailsTask
        ];

        Action[] assignments;

        try
        {
            assignments = await Task.WhenAll(loads);
        }
        catch
        {
            var failures = loads
                .Where(task => task.IsFaulted)
                .SelectMany(task =>
                    task.Exception!.Flatten().InnerExceptions)
                .ToList();

            Log.Error(
                $"[Dashboard] Customer #{customerId}: {failures.Count} load(s) failed");

            throw new AggregateException(
                $"Customer #{customerId} dashboard could not be loaded.",
                failures);
        }

        foreach (var assignment in assignments)
        {
            assignment();
        }

        Log.Success(
            $"[Dashboard] Customer #{customerId}: all four loads completed");

        return dashboard;
    }

    private static async Task<Action> LoadAndAssignAsync<T>(
        Func<Task<T>> load,
        Action<T> assign)
    {
        T result = await load();

        return () => assign(result);
    }

    private async Task<List<PaymentTransaction>> LoadPaymentsAsync(
        int customerId,
        CancellationToken ct)
    {
        Log.Info($"[Dashboard] Opening payments connection for Customer #{customerId}");

        await Task.Delay(200, ct);

        Log.Info($"[Dashboard] Querying payments for Customer #{customerId}");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        if (_failPaymentsForCustomerIds.Contains(customerId))
        {
            throw new TimeoutException("Payments store timeout");
        }

        var orders = await _store.ReadAsync<Order>(
            DataFiles.Orders,
            ct);

        var orderIds = orders
            .Where(o => o.CustomerId == customerId)
            .Select(o => o.Id)
            .ToHashSet();

        var payments = await _store.ReadAsync<PaymentTransaction>(
            DataFiles.Payments,
            ct);

        return payments
            .Where(p => orderIds.Contains(p.OrderId))
            .ToList();
    }

    private async Task<List<EmailMessage>> LoadEmailsAsync(
        int customerId,
        CancellationToken ct)
    {
        Log.Info($"[Dashboard] Opening emails connection for Customer #{customerId}");

        await Task.Delay(200, ct);

        Log.Info($"[Dashboard] Querying emails for Customer #{customerId}");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var orders = await _store.ReadAsync<Order>(
            DataFiles.Orders,
            ct);

        var orderIds = orders
            .Where(o => o.CustomerId == customerId)
            .Select(o => o.Id)
            .ToHashSet();

        var emails = await _store.ReadAsync<EmailMessage>(
            DataFiles.Emails,
            ct);

        return emails
            .Where(e => orderIds.Contains(e.OrderId))
            .ToList();
    }
}