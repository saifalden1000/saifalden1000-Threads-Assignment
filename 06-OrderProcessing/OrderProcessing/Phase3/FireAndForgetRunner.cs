using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class FireAndForgetRunner
{
    private readonly JsonFileStore _store;

    public FireAndForgetRunner(JsonFileStore store)
    {
        _store = store;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var seeder = new DataSeeder(_store);
        await seeder.ResetAsync(ct);

        var incomingRepository = new IncomingOrderRepository(_store);
        var orders = await incomingRepository.GetAllAsync(ct);

        var order17 = orders.First(o => o.Id == 17);
        var order18 = orders.First(o => o.Id == 18);

        order17.Status = OrderStatus.Completed;
        order18.Status = OrderStatus.Completed;

        var customerService = new CustomerService(_store);
        var customer17 = await customerService.GetCustomerAsync(7, ct);
        var customer18 = await customerService.GetCustomerAsync(8, ct);

        var orderRepository = new OrderRepository(_store);

        var emailService = new EmailService(
            _store,
            new Dictionary<int, int>
            {
                [17] = int.MaxValue,
                [18] = int.MaxValue
            });

        var demo = new FireAndForgetDemo(
            orderRepository,
            emailService);

        Console.WriteLine();
        Console.WriteLine("===== Broken Fire-and-Forget Demo =====");

        bool unobservedExceptionDetected = false;

        EventHandler<UnobservedTaskExceptionEventArgs> handler =
            (_, eventArgs) =>
            {
                unobservedExceptionDetected = true;

                Log.Error(
                    $"Unobserved task exception: {eventArgs.Exception.GetBaseException().Message}");

                eventArgs.SetObserved();
            };

        TaskScheduler.UnobservedTaskException += handler;

        try
        {
            await demo.ProcessOrderAsync(
                order17,
                customer17,
                ct);

            Console.WriteLine(
                "Caller continued: ProcessOrderAsync returned before email completed.");

            await Task.Delay(1500, ct);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Console.WriteLine(
                $"Unobserved exception detected: {unobservedExceptionDetected}");
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }

        Console.WriteLine();
        Console.WriteLine("===== Fixed Await Demo =====");

        try
        {
            await demo.ProcessOrderFixedAsync(
                order18,
                customer18,
                ct);
        }
        catch (SmtpUnavailableException ex)
        {
            Console.WriteLine(
                $"Caller successfully observed the email exception: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Fire-and-forget investigation completed.");
    }
}