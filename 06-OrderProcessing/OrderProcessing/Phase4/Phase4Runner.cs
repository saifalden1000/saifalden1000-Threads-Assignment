using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class Phase4Runner
{
    private readonly JsonFileStore _store;

    public Phase4Runner(JsonFileStore store)
    {
        _store = store;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("             PHASE 4");
        Console.WriteLine("========================================");

        Console.WriteLine();
        Console.WriteLine("Starting Tasks 10.1 and 10.2...");

        var dashboardDemo = new CustomerDashboardDemo(_store);
        await dashboardDemo.RunAsync(ct);

        ct.ThrowIfCancellationRequested();

        Console.WriteLine();
        Console.WriteLine("Starting Task 10.3: Shipping Quote Race...");

        var shippingDemo = new ShippingQuoteDemo(_store);
        await shippingDemo.RunAsync(ct);

        ct.ThrowIfCancellationRequested();

        Console.WriteLine();
        Console.WriteLine("Starting Task 10.4A: Parallel Fraud Scoring...");

        var fraudScoringService = new FraudScoringService();
        await fraudScoringService.RunAsync(ct);

        ct.ThrowIfCancellationRequested();

        Console.WriteLine();
        Console.WriteLine("Starting Task 10.4B: Parallel Email Processing...");

        var parallelEmailDemo = new ParallelEmailDemo(_store);
        await parallelEmailDemo.RunAsync(ct);

        Console.WriteLine();
        Console.WriteLine("========================================");
        Console.WriteLine("          PHASE 4 COMPLETED");
        Console.WriteLine("========================================");
    }
}