using System.Diagnostics;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class Phase1Runner
{
    private readonly DataSeeder _seeder;
    private readonly IncomingOrderRepository _incomingOrderRepository;
    private readonly OrderRepository _orderRepository;
    private readonly SyncOrderValidator _syncValidator;
    private readonly OrderValidator _asyncValidator;

    public Phase1Runner(JsonFileStore store)
    {
        _seeder = new DataSeeder(store);
        _incomingOrderRepository = new IncomingOrderRepository(store);
        _orderRepository = new OrderRepository(store);

        var syncCustomerRepository = new SyncCustomerRepository(store);
        var syncProductRepository = new SyncProductRepository(store);
        var syncCouponRepository = new SyncCouponRepository(store);

        _syncValidator = new SyncOrderValidator(
            syncCustomerRepository,
            syncProductRepository,
            syncCouponRepository);

        var customerRepository = new CustomerRepository(store);
        var productRepository = new ProductRepository(store);
        var couponRepository = new CouponRepository(store);

        _asyncValidator = new OrderValidator(
            customerRepository,
            productRepository,
            couponRepository);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await _seeder.ResetAsync(cancellationToken);

        var incomingOrders = await _incomingOrderRepository.GetAllAsync(
            cancellationToken);

        Log.Info("Starting synchronous validation");

        var syncStopwatch = Stopwatch.StartNew();

        foreach (var order in incomingOrders)
        {
            _syncValidator.ValidateOrder(order);
        }

        syncStopwatch.Stop();

        await _seeder.ResetAsync(cancellationToken);

        Log.Info("Starting asynchronous validation");

        var asyncValidationStopwatch = new Stopwatch();
        var asyncFullStopwatch = Stopwatch.StartNew();

        foreach (var order in incomingOrders)
        {
            asyncValidationStopwatch.Start();

            var validatedOrder = await _asyncValidator.ValidateOrderAsync(
                order,
                cancellationToken);

            asyncValidationStopwatch.Stop();

            await _orderRepository.SaveAsync(
                validatedOrder,
                cancellationToken);
        }

        asyncFullStopwatch.Stop();

        var validatedOrders = await _orderRepository.GetByStatusAsync(
            OrderStatus.Validated,
            cancellationToken);

        var rejectedOrders = await _orderRepository.GetByStatusAsync(
            OrderStatus.Rejected,
            cancellationToken);

        var allOrders = await _orderRepository.GetAllAsync(cancellationToken);

        Console.WriteLine();
        Console.WriteLine("===== Validation Summary =====");
        Console.WriteLine($"Total Orders: {allOrders.Count}");
        Console.WriteLine($"Validated:    {validatedOrders.Count}");
        Console.WriteLine($"Rejected:     {rejectedOrders.Count}");

        foreach (var order in rejectedOrders.OrderBy(order => order.Id))
        {
            Console.WriteLine($"  - Order #{order.Id}: {order.FailureReason}");
        }

        Console.WriteLine($"Sync Time (validation): {syncStopwatch.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"Async Time (validation): {asyncValidationStopwatch.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"Async Full Time (with saves): {asyncFullStopwatch.Elapsed.TotalSeconds:F1} seconds");
        Console.WriteLine($"orders.json now contains {allOrders.Count} records.");
    }
}