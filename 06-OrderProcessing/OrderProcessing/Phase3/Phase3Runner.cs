using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace OrderProcessing.Phase3;

public sealed class Phase3Runner
{
    private readonly DataSeeder _seeder;
    private readonly JsonFileStore _store;
    private readonly IncomingOrderRepository _incomingOrderRepository;
    private readonly OrderRepository _orderRepository;
    private readonly OrderProcessor _orderProcessor;

    public Phase3Runner(JsonFileStore store)
    {
        _store = store;

        _seeder = new DataSeeder(store);
        _incomingOrderRepository = new IncomingOrderRepository(store);

        var customerRepository = new CustomerRepository(store);
        var productRepository = new ProductRepository(store);
        var couponRepository = new CouponRepository(store);

        _orderRepository = new OrderRepository(
            store,
            new[] { 13 });

        var customerService = new CustomerService(store);
        var inventoryService = new InventoryService(productRepository);

        var pricingService = new PricingService(
            store,
            customerRepository,
            couponRepository);

        IPaymentGateway paymentGateway = new PaymentGateway(
            store,
            new[] { 5 },
            new[] { 8 });

        var emailService = new EmailService(
            store,
            new Dictionary<int, int>
            {
                [3] = 2,
                [17] = int.MaxValue
            });

        _orderProcessor = new OrderProcessor(
            customerService,
            inventoryService,
            pricingService,
            paymentGateway,
            emailService,
            _orderRepository,
            productRepository);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("========== PHASE 3: FULL CHECKOUT ==========");

        await RunComparisonAsync(ct);

        await _seeder.ResetAsync(ct);

        var incomingOrders = await _incomingOrderRepository.GetAllAsync(ct);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var processingTasks = new List<Task<bool>>();

        foreach (var order in incomingOrders)
        {
            processingTasks.Add(
                _orderProcessor.ProcessOrderAsync(order, ct));
        }

        Console.WriteLine(
            $"Started checkout tasks: {processingTasks.Count}");

        for (int i = 0; i < processingTasks.Count; i++)
        {
            try
            {
                await processingTasks[i];
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"Unexpected error processing Order #{incomingOrders[i].Id}: {ex.Message}");
            }
        }

        stopwatch.Stop();

        var orders = await _orderRepository.GetAllAsync(ct);

        var payments = await _store.ReadAsync<PaymentTransaction>(
            DataFiles.Payments,
            ct);

        var emails = await _store.ReadAsync<EmailMessage>(
            DataFiles.Emails,
            ct);

        var retryEmails = await _store.ReadAsync<EmailMessage>(
            DataFiles.EmailRetry,
            ct);

        var products = await _store.ReadAsync<Product>(
            DataFiles.Products,
            ct);

        Console.WriteLine();
        Console.WriteLine("========== FINAL ORDER RESULTS ==========");

        Console.WriteLine(
            $"{"Order",-8} {"Status",-15} {"Total",-12} Reason");

        foreach (var order in orders.OrderBy(o => o.Id))
        {
            Console.WriteLine(
                $"#{order.Id,-7} {order.Status,-15} " +
                $"{order.TotalAmount,-12:F2} {order.FailureReason}");
        }

        Console.WriteLine();
        Console.WriteLine("========== PHASE 3 SUMMARY ==========");

        Console.WriteLine($"Total orders: {orders.Count}");
        Console.WriteLine(
            $"Completed: {orders.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine(
            $"Rejected: {orders.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine(
            $"Payment Failed: {orders.Count(o => o.Status == OrderStatus.PaymentFailed)}");
        Console.WriteLine(
            $"Failed: {orders.Count(o => o.Status == OrderStatus.Failed)}");

        Console.WriteLine();
        Console.WriteLine($"Payment records: {payments.Count}");
        Console.WriteLine(
            $"Captured: {payments.Count(p => p.Status == PaymentStatus.Captured)}");
        Console.WriteLine(
            $"Declined: {payments.Count(p => p.Status == PaymentStatus.Declined)}");
        Console.WriteLine(
            $"Refunded: {payments.Count(p => p.Status == PaymentStatus.Refunded)}");

        Console.WriteLine();
        Console.WriteLine($"Emails sent: {emails.Count}");
        Console.WriteLine($"Emails queued for retry: {retryEmails.Count}");

        foreach (var email in emails.OrderBy(e => e.OrderId))
        {
            Console.WriteLine(
                $"Email for Order #{email.OrderId}: Attempts={email.Attempts}");
        }

        foreach (var email in retryEmails)
        {
            Console.WriteLine(
                $"Retry Order #{email.OrderId}: Attempts={email.Attempts}, " +
                $"LastError={email.LastError}");
        }

        var keyboard = products.FirstOrDefault(p => p.Id == 4);

        Console.WriteLine();
        Console.WriteLine(
            $"Mechanical Keyboard remaining stock: {keyboard?.StockQuantity}");

        Console.WriteLine(
            $"Total processing time: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine(
            $"orders.json contains {orders.Count} records.");
    }
    private async Task RunComparisonAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("========== SYNC VS ASYNC COMPARISON ==========");

        await _seeder.ResetAsync(ct);

        var syncOrders = (await _incomingOrderRepository.GetAllAsync(ct))
            .Where(o => o.Id <= 10)
            .OrderBy(o => o.Id)
            .ToList();

        var syncOrderRepository = new OrderRepository(_store);

        var syncCustomerRepository = new SyncCustomerRepository(_store);
        var syncProductRepository = new SyncProductRepository(_store);
        var syncCouponRepository = new SyncCouponRepository(_store);

        var syncProcessor = new SyncOrderProcessor(
            _store,
            new SyncCustomerService(_store),
            new SyncInventoryService(_store),
            new SyncPricingService(
                _store,
                syncCustomerRepository,
                syncProductRepository,
                syncCouponRepository),
            new SyncPaymentGateway(_store),
            new SyncEmailService(_store),
            syncOrderRepository);

        var syncErrors = new ConcurrentQueue<Exception>();
        var syncThreads = new List<Thread>();

        Log.ResetThreadTracking();

        int syncPoolBefore = ThreadPool.ThreadCount;

        var syncStopwatch = Stopwatch.StartNew();

        foreach (var order in syncOrders)
        {
            var currentOrder = order;

            var thread = new Thread(() =>
            {
                try
                {
                    syncProcessor.ProcessOrder(currentOrder);
                }
                catch (Exception ex)
                {
                    syncErrors.Enqueue(ex);
                }
            });

            syncThreads.Add(thread);
            thread.Start();
        }

        foreach (var thread in syncThreads)
        {
            thread.Join();
        }

        syncStopwatch.Stop();

        int syncDistinctThreads = Log.DistinctThreadCount;
        int syncPoolAfter = ThreadPool.ThreadCount;

        var syncResults = _store.Read<Order>(DataFiles.Orders);

        Console.WriteLine();
        Console.WriteLine("----- Synchronous Results -----");
        Console.WriteLine($"Orders processed: {syncResults.Count}");
        Console.WriteLine(
            $"Completed: {syncResults.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine(
            $"Rejected: {syncResults.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine(
            $"Time: {syncStopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Dedicated threads used: {syncDistinctThreads}");
        Console.WriteLine(
            $"ThreadPool.ThreadCount: {syncPoolBefore} -> {syncPoolAfter}");
        Console.WriteLine($"Unexpected errors: {syncErrors.Count}");

        await _seeder.ResetAsync(ct);

        var asyncOrders = (await _incomingOrderRepository.GetAllAsync(ct))
            .Where(o => o.Id <= 10)
            .OrderBy(o => o.Id)
            .ToList();

        var asyncProductRepository = new ProductRepository(_store);
        var asyncCustomerRepository = new CustomerRepository(_store);
        var asyncCouponRepository = new CouponRepository(_store);
        var asyncOrderRepository = new OrderRepository(_store);

        var asyncProcessor = new OrderProcessor(
            new CustomerService(_store),
            new InventoryService(asyncProductRepository),
            new PricingService(
                _store,
                asyncCustomerRepository,
                asyncCouponRepository),
            new PaymentGateway(
                _store,
                Array.Empty<int>(),
                Array.Empty<int>()),
            new EmailService(
                _store,
                new Dictionary<int, int>()),
            asyncOrderRepository,
            asyncProductRepository);

        var asyncErrors = new List<Exception>();

        Log.ResetThreadTracking();

        int asyncPoolBefore = ThreadPool.ThreadCount;

        var asyncStopwatch = Stopwatch.StartNew();

        var asyncTasks = new List<Task<bool>>();

        foreach (var order in asyncOrders)
        {
            asyncTasks.Add(
                asyncProcessor.ProcessOrderAsync(order, ct));
        }

        foreach (var task in asyncTasks)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                asyncErrors.Add(ex);
            }
        }

        asyncStopwatch.Stop();

        int asyncDistinctThreads = Log.DistinctThreadCount;
        int asyncPoolAfter = ThreadPool.ThreadCount;

        var asyncResults = await asyncOrderRepository.GetAllAsync(ct);

        Console.WriteLine();
        Console.WriteLine("----- Asynchronous Results -----");
        Console.WriteLine($"Orders processed: {asyncResults.Count}");
        Console.WriteLine(
            $"Completed: {asyncResults.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine(
            $"Rejected: {asyncResults.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine(
            $"Time: {asyncStopwatch.Elapsed.TotalSeconds:F2} seconds");
        Console.WriteLine($"Distinct threads used: {asyncDistinctThreads}");
        Console.WriteLine(
            $"ThreadPool.ThreadCount: {asyncPoolBefore} -> {asyncPoolAfter}");
        Console.WriteLine($"Unexpected errors: {asyncErrors.Count}");

        await _seeder.ResetAsync(ct);

        Console.WriteLine();
        Console.WriteLine("Sync vs Async comparison completed.");
        Console.WriteLine("Restored seed data for the full Phase 3 checkout.");
    }
}