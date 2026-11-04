using System.Diagnostics;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase4;

public sealed class ShippingQuoteService
{
    private readonly List<IShippingCarrier> _carriers;

    public ShippingQuoteService(IEnumerable<IShippingCarrier> carriers)
    {
        _carriers = carriers.ToList();
    }

    public async Task<ShippingQuote> GetFastestQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        const int timeLimitMs = 1200;

        using var cancellationSource =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        var stopwatch = Stopwatch.StartNew();

        var tasks = _carriers
            .Select(carrier => (
                Carrier: carrier,
                Task: GetQuoteSafelyAsync(
                    carrier,
                    order,
                    cancellationSource.Token)))
            .ToList();

        Task timeoutTask = Task.Delay(timeLimitMs, ct);

        var pending = tasks.ToList();

        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();

            Task completed = await Task.WhenAny(
                pending.Select(item => (Task)item.Task)
                    .Append(timeoutTask));

            ct.ThrowIfCancellationRequested();

            if (completed == timeoutTask)
                break;

            var finished = pending.First(
                item => item.Task == completed);

            pending.Remove(finished);

            try
            {
                decimal price = await finished.Task;

                stopwatch.Stop();

                var cancelledCarriers = tasks
                    .Where(item =>
                        item.Carrier != finished.Carrier &&
                        !item.Task.IsCompleted)
                    .Select(item => item.Carrier.Name)
                    .ToList();

                cancellationSource.Cancel();

                await ObserveRemainingTasksAsync(tasks);

                Log.Success(
                    $"[{finished.Carrier.Name}] Selected for Order #{order.Id}");

                return new ShippingQuote
                {
                    Carrier = finished.Carrier.Name,
                    Price = price,
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    CancelledCarriers = cancelledCarriers
                };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"[{finished.Carrier.Name}] Failed for Order #{order.Id}: {ex.Message}");
            }
        }

        ct.ThrowIfCancellationRequested();

        if (pending.Count == 0)
        {
            cancellationSource.Cancel();

            await ObserveRemainingTasksAsync(tasks);

            throw new ShippingProviderUnavailableException(
                "All carriers failed");
        }

        var cancelled = pending
            .Where(item => !item.Task.IsCompleted)
            .Select(item => item.Carrier.Name)
            .ToList();

        cancellationSource.Cancel();

        await ObserveRemainingTasksAsync(tasks);

        stopwatch.Stop();

        Log.Info(
            $"[ShippingQuoteService] Order #{order.Id}: using flat rate after {timeLimitMs} ms");

        return new ShippingQuote
        {
            Carrier = "Flat rate",
            Price = 25.00m,
            ElapsedMs = stopwatch.ElapsedMilliseconds,
            CancelledCarriers = cancelled
        };
    }

    private static async Task<decimal> GetQuoteSafelyAsync(
        IShippingCarrier carrier,
        Order order,
        CancellationToken ct)
    {
        return await carrier.GetQuoteAsync(order, ct);
    }

    private static async Task ObserveRemainingTasksAsync(
        IEnumerable<(IShippingCarrier Carrier, Task<decimal> Task)> tasks)
    {
        foreach (var item in tasks)
        {
            try
            {
                await item.Task;
            }
            catch
            {
            }
        }
    }
}