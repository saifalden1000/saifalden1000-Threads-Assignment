using System.Collections.Concurrent;
using System.Diagnostics;
using OrderProcessing.Shared.Logging;

namespace OrderProcessing.Phase4;

public sealed class FraudScoringService
{
    private const int OrderCount = 10_000;

    public async Task RunAsync(CancellationToken ct)
    {
        Log.Info("========== FRAUD SCORING ==========");
        Log.Info($"Orders: {OrderCount}");
        Log.Info($"Available processor cores: {Environment.ProcessorCount}");

        var sequentialFlags = new List<int>();

        var sequentialStopwatch = Stopwatch.StartNew();

        for (int orderId = 1; orderId <= OrderCount; orderId++)
        {
            ct.ThrowIfCancellationRequested();

            if (Score(orderId) >= 90)
            {
                sequentialFlags.Add(orderId);
            }
        }

        sequentialStopwatch.Stop();

        var parallelFlags = new ConcurrentBag<int>();
        var parallelThreadIds = new ConcurrentDictionary<int, byte>();

        var parallelStopwatch = Stopwatch.StartNew();

        await Task.Run(() =>
        {
            Parallel.ForEach(
                Enumerable.Range(1, OrderCount),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = ct
                },
                orderId =>
                {
                    parallelThreadIds.TryAdd(
                        Environment.CurrentManagedThreadId,
                        0);

                    if (Score(orderId) >= 90)
                    {
                        parallelFlags.Add(orderId);
                    }
                });
        }, ct);

        parallelStopwatch.Stop();

        var sequentialSorted = sequentialFlags.Order().ToList();
        var parallelSorted = parallelFlags.Order().ToList();

        double speedup = parallelStopwatch.Elapsed.TotalMilliseconds == 0
            ? 0
            : sequentialStopwatch.Elapsed.TotalMilliseconds /
              parallelStopwatch.Elapsed.TotalMilliseconds;

        Log.Success(
            $"Sequential: {sequentialSorted.Count} flagged in " +
            $"{sequentialStopwatch.Elapsed.TotalSeconds:F2} seconds");

        Log.Success(
            $"Parallel: {parallelSorted.Count} flagged in " +
            $"{parallelStopwatch.Elapsed.TotalSeconds:F2} seconds");

        Log.Info($"Speed-up: {speedup:F2}x");
        Log.Info($"Parallel threads used: {parallelThreadIds.Count}");
        Log.Info($"Processor cores available: {Environment.ProcessorCount}");

        Log.Info(
            $"First five flagged IDs: " +
            $"{string.Join(", ", parallelSorted.Take(5))}");

        Log.Info(
            $"Sequential and parallel results match: " +
            $"{sequentialSorted.SequenceEqual(parallelSorted)}");
    }

    private static int Score(int orderId)
    {
        long h = orderId;

        for (int i = 0; i < 100_000; i++)
        {
            h = (h * 31 + i) % 1_000_000_007;
        }

        return (int)(h % 100);
    }
}