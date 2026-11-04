using OrderProcessing.Phase1;
using OrderProcessing.Phase3;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class LiveCheckoutProgressRunner
{
    private readonly JsonFileStore _store;
    private readonly DataSeeder _seeder;

    public LiveCheckoutProgressRunner(JsonFileStore store)
    {
        _store = store;
        _seeder = new DataSeeder(store);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("========== TASK 10.1: LIVE CHECKOUT PROGRESS ==========");

        await _seeder.ResetAsync(ct);

        var incomingRepository = new IncomingOrderRepository(_store);
        var incomingOrders = await incomingRepository.GetAllAsync(ct);

        var customerRepository = new CustomerRepository(_store);
        var productRepository = new ProductRepository(_store);
        var couponRepository = new CouponRepository(_store);
        var orderRepository = new OrderRepository(_store, new[] { 13 });

        var processor = new OrderProcessor(
            new CustomerService(_store),
            new InventoryService(productRepository),
            new PricingService(
                _store,
                customerRepository,
                couponRepository),
            new PaymentGateway(
                _store,
                new[] { 5 },
                new[] { 8 }),
            new EmailService(
                _store,
                new Dictionary<int, int>
                {
                    [3] = 2,
                    [17] = int.MaxValue
                }),
            orderRepository,
            productRepository);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var taskOrders = new Dictionary<Task<bool>, Order>();

        foreach (var order in incomingOrders)
        {
            if (ct.IsCancellationRequested)
                break;

            Task<bool> task = processor.ProcessOrderAsync(order, ct);
            taskOrders.Add(task, order);
        }

        int completedCount = 0;

        await foreach (var completedTask in Task.WhenEach(taskOrders.Keys))
        {
            var order = taskOrders[completedTask];

            try
            {
                await completedTask;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Log.Info(
                    $"Order #{order.Id} finished cancellation cleanup");
            }
            catch (Exception ex) when (ct.IsCancellationRequested)
            {
                Log.Error(
                    $"Order #{order.Id} stopped during cancellation: {ex.Message}");
            }
            catch (Exception ex)
            {
                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;

                await orderRepository.SaveAsync(order, ct);

                Log.Error(
                    $"Unexpected error processing Order #{order.Id}: {ex.Message}");
            }

            completedCount++;

            Console.WriteLine(
                $"[{completedCount,2}/20] Order #{order.Id} -> " +
                $"{order.Status} " +
                $"({order.FailureReason ?? "No errors"}) " +
                $"after {stopwatch.Elapsed.TotalSeconds:F1} s");
        }

        stopwatch.Stop();

        var finalOrders = await orderRepository.GetAllAsync(
            CancellationToken.None);

        Console.WriteLine();
        Console.WriteLine("========== LIVE CHECKOUT SUMMARY ==========");
        Console.WriteLine($"Total orders saved: {finalOrders.Count}");
        Console.WriteLine(
            $"Completed: {finalOrders.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine(
            $"Rejected: {finalOrders.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine(
            $"Payment Failed: {finalOrders.Count(o => o.Status == OrderStatus.PaymentFailed)}");
        Console.WriteLine(
            $"Failed: {finalOrders.Count(o => o.Status == OrderStatus.Failed)}");
        Console.WriteLine(
            $"Pending: {finalOrders.Count(o => o.Status == OrderStatus.Pending)}");
        Console.WriteLine(
            $"Cancelled: {finalOrders.Count(o => o.FailureReason == "Cancelled")}");
        Console.WriteLine(
            $"Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");
    }
}