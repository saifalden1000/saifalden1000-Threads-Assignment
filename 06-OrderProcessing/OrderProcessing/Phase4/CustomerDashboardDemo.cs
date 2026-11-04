using System.Diagnostics;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class CustomerDashboardDemo
{
    private readonly LiveCheckoutProgressRunner _checkoutRunner;
    private readonly CustomerDashboardService _dashboardService;

    public CustomerDashboardDemo(JsonFileStore store)
    {
        _checkoutRunner = new LiveCheckoutProgressRunner(store);

        _dashboardService = new CustomerDashboardService(
            store,
            new[] { 99 });
    }

    public async Task RunAsync(CancellationToken ct)
    {
        await _checkoutRunner.RunAsync(ct);
        ct.ThrowIfCancellationRequested();

        Console.WriteLine();
        Console.WriteLine("========== TASK 10.2: CUSTOMER DASHBOARD ==========");

        var stopwatch = Stopwatch.StartNew();

        var dashboard = await _dashboardService.LoadAsync(3, ct);

        stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine("----- Customer #3 Dashboard -----");
        Console.WriteLine($"Name: {dashboard.Customer.Name}");
        Console.WriteLine($"Orders: {dashboard.Orders.Count}");

        foreach (var order in dashboard.Orders.OrderBy(o => o.Id))
        {
            Console.WriteLine(
                $"Order #{order.Id}: {order.Status}");
        }

        Console.WriteLine($"Payments: {dashboard.Payments.Count}");

        foreach (var payment in dashboard.Payments)
        {
            Console.WriteLine(
                $"Order #{payment.OrderId}: {payment.Status}");
        }

        Console.WriteLine($"Emails: {dashboard.Emails.Count}");
        Console.WriteLine(
            $"Dashboard load time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine();
        Console.WriteLine("----- Customer #99 Dashboard Failure Test -----");

        try
        {
            await _dashboardService.LoadAsync(99, ct);
        }
        catch (AggregateException ex)
        {
            foreach (var failure in ex.Flatten().InnerExceptions)
            {
                Log.Error($"Dashboard load failure: {failure.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("Customer dashboard tests completed.");
    }
}