using System.Diagnostics;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class Phase2Runner
{
    private readonly DataSeeder _seeder;
    private readonly IncomingOrderRepository _incomingOrderRepository;
    private readonly OrderRepository _orderRepository;
    private readonly ProductRepository _productRepository;
    private readonly OrderValidator _orderValidator;
    private readonly OrderPricer _orderPricer;
    private readonly RaceConditionDemo _raceConditionDemo;
    private readonly OrderProcessorV2 _orderProcessor;
    private readonly ReportGenerator _reportGenerator;

    public Phase2Runner(JsonFileStore store)
    {
        _seeder = new DataSeeder(store);

        var customerRepository = new CustomerRepository(store);
        _productRepository = new ProductRepository(store);
        var couponRepository = new CouponRepository(store);

        _orderRepository = new OrderRepository(store);
        _incomingOrderRepository = new IncomingOrderRepository(store);

        _orderValidator = new OrderValidator(
            customerRepository,
            _productRepository,
            couponRepository);

        var pricingService = new PricingService(
            store,
            customerRepository,
            couponRepository,
            new[] { 7 });

        _orderPricer = new OrderPricer(pricingService);

        _raceConditionDemo = new RaceConditionDemo(
            store,
            _productRepository);

        var invoiceGenerator = new InvoiceGenerator();

        _orderProcessor = new OrderProcessorV2(
            _productRepository,
            _orderPricer,
            invoiceGenerator,
            _orderRepository);

        _reportGenerator = new ReportGenerator(
            _orderRepository,
            _productRepository);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("========== PHASE 2 ==========");

        await _seeder.ResetAsync(ct);

        await _raceConditionDemo.RunAsync(ct);

        await _seeder.ResetAsync(ct);

        var incomingOrders = await _incomingOrderRepository.GetAllAsync(ct);

        var sequentialOrder = CloneOrder(
            incomingOrders.First(o => o.Id == 1));

        var concurrentOrder = CloneOrder(
            incomingOrders.First(o => o.Id == 1));

        Console.WriteLine();
        Console.WriteLine("===== Pricing Comparison: Order #1 =====");

        var sequentialStopwatch = Stopwatch.StartNew();

        await _orderPricer.PriceOrderSequentialAsync(
            sequentialOrder,
            ct);

        sequentialStopwatch.Stop();

        var concurrentStopwatch = Stopwatch.StartNew();

        await _orderPricer.PriceOrderAsync(
            concurrentOrder,
            ct);

        concurrentStopwatch.Stop();

        Console.WriteLine(
            $"Sequential pricing: {sequentialStopwatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine(
            $"Concurrent pricing: {concurrentStopwatch.Elapsed.TotalSeconds:F2} seconds");

        PrintPricingResult("Sequential", sequentialOrder);
        PrintPricingResult("Concurrent", concurrentOrder);

        var phase2Stopwatch = Stopwatch.StartNew();

        Console.WriteLine();
        Console.WriteLine("===== Concurrent Validation =====");

        var validationTasks = new List<Task<Order>>();

        foreach (var order in incomingOrders)
        {
            validationTasks.Add(
                _orderValidator.ValidateOrderAsync(order, ct));
        }

        foreach (var validationTask in validationTasks)
        {
            var validatedOrder = await validationTask;

            await _orderRepository.SaveAsync(validatedOrder, ct);
        }

        var validatedOrders = await _orderRepository.GetByStatusAsync(
            OrderStatus.Validated,
            ct);

        Console.WriteLine(
            $"Validated orders ready for processing: {validatedOrders.Count}");

        Console.WriteLine();
        Console.WriteLine("===== Concurrent Order Processing =====");

        var processingTasks = new List<Task<bool>>();

        foreach (var order in validatedOrders)
        {
            processingTasks.Add(
                _orderProcessor.ProcessValidatedOrderAsync(order, ct));
        }

        Console.WriteLine(
            $"First task status immediately after creation: {processingTasks[0].Status}");

        await Task.Delay(1000, ct);

        Console.WriteLine();
        Console.WriteLine("Task statuses after 1 second:");

        for (int i = 0; i < processingTasks.Count; i++)
        {
            Console.WriteLine(
                $"Order #{validatedOrders[i].Id}: {processingTasks[i].Status}");
        }

        Console.WriteLine();
        Console.WriteLine("===== Processing Results =====");

        for (int i = 0; i < processingTasks.Count; i++)
        {
            var order = validatedOrders[i];
            var task = processingTasks[i];

            try
            {
                bool completed = await task;

                Console.WriteLine(
                    $"Order #{order.Id}: {task.Status}, Result: {completed}");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Order #{order.Id}: {task.Status}, Error: {ex.Message}");

                foreach (var item in order.Items)
                {
                    await _productRepository.ReleaseStockAsync(
                        item.ProductId,
                        item.Quantity,
                        ct);
                }

                order.Status = OrderStatus.Failed;
                order.FailureReason = ex.Message;

                await _orderRepository.SaveAsync(order, ct);
            }
        }

        phase2Stopwatch.Stop();

        Console.WriteLine();
        Console.WriteLine("===== Final Task Statuses =====");

        for (int i = 0; i < processingTasks.Count; i++)
        {
            Console.WriteLine(
                $"Order #{validatedOrders[i].Id}: {processingTasks[i].Status}");
        }

        var allOrders = await _orderRepository.GetAllAsync(ct);

        Console.WriteLine();
        Console.WriteLine("===== Phase 2 Summary =====");
        Console.WriteLine($"Total Orders: {allOrders.Count}");
        Console.WriteLine(
            $"Completed: {allOrders.Count(o => o.Status == OrderStatus.Completed)}");
        Console.WriteLine(
            $"Rejected: {allOrders.Count(o => o.Status == OrderStatus.Rejected)}");
        Console.WriteLine(
            $"Failed: {allOrders.Count(o => o.Status == OrderStatus.Failed)}");
        Console.WriteLine(
            $"Processing Time: {phase2Stopwatch.Elapsed.TotalSeconds:F2} seconds");

        Console.WriteLine();
        Console.WriteLine("Generating report...");

        Task<Report> reportTask = _reportGenerator.GenerateAsync(ct);

        Report report = await reportTask;

        Console.WriteLine();
        Console.WriteLine("===== Phase 2 Report =====");
        Console.WriteLine($"Total Orders: {report.TotalOrders}");
        Console.WriteLine($"Completed: {report.CompletedCount}");
        Console.WriteLine($"Rejected: {report.RejectedCount}");
        Console.WriteLine($"Failed: {report.FailedCount}");
        Console.WriteLine($"Payment Failed: {report.PaymentFailedCount}");
        Console.WriteLine($"Total Revenue: {report.TotalRevenue:F2}");
        Console.WriteLine($"Average Order Value: {report.AverageOrderValue:F2}");
        Console.WriteLine(
            $"Top Selling Product: {report.TopSellingProduct}");
        Console.WriteLine(
            $"Top Selling Quantity: {report.TopSellingQuantity}");

        Console.WriteLine();
        Console.WriteLine($"orders.json contains {allOrders.Count} records.");
    }

    private static Order CloneOrder(Order source)
    {
        return new Order
        {
            Id = source.Id,
            CustomerId = source.CustomerId,
            Items = source.Items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice
            }).ToList(),
            CouponCode = source.CouponCode,
            SubTotal = source.SubTotal,
            Discount = source.Discount,
            ShippingCost = source.ShippingCost,
            TotalAmount = source.TotalAmount,
            Status = source.Status,
            CreatedAt = source.CreatedAt,
            FailureReason = source.FailureReason,
            TransactionId = source.TransactionId
        };
    }

    private static void PrintPricingResult(string label, Order order)
    {
        Console.WriteLine(
            $"{label}: Subtotal={order.SubTotal:F2}, " +
            $"Discount={order.Discount:F2}, " +
            $"Shipping={order.ShippingCost:F2}, " +
            $"Total={order.TotalAmount:F2}");
    }
}