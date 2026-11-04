using OrderProcessing.Phase1;
using OrderProcessing.Phase2;
using OrderProcessing.Phase3;
using OrderProcessing.Phase4;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing;

internal class Program
{
    private static CancellationTokenSource? _activeRunCancellation;

    private static async Task Main(string[] args)
    {
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Volatile.Read(ref _activeRunCancellation)?.Cancel();
        };

        var parsed = ParseArgs(args);

        if (parsed.Error is not null)
        {
            Console.WriteLine(parsed.Error);
            return;
        }

        var store = new JsonFileStore();
        var mode = parsed.Mode;

        if (string.IsNullOrWhiteSpace(mode))
        {
            Console.WriteLine("===== Order Processing System =====");
            Console.WriteLine("1. Phase 1 - Async Data Access Layer");
            Console.WriteLine("2. Phase 2 - Concurrent Processing");
            Console.WriteLine("3. Phase 3 - Full Checkout Workflow");
            Console.WriteLine("4. Fire-and-Forget Investigation");
            Console.WriteLine("5. Phase 4 - Advanced Concurrency");
            Console.WriteLine("6. Run All Phases");
            Console.WriteLine("0. Exit");
            Console.Write("Select an option: ");

            mode = Console.ReadLine()?.ToLowerInvariant();
        }

        switch (mode)
        {
            case "1":
            case "phase1":
                await RunPhaseAsync(
                    "Phase 1",
                    ct => new Phase1Runner(store).RunAsync(ct),
                    parsed.CancelAfterMs);
                break;

            case "2":
            case "phase2":
                await RunPhaseAsync(
                    "Phase 2",
                    ct => new Phase2Runner(store).RunAsync(ct),
                    parsed.CancelAfterMs);
                break;

            case "3":
            case "phase3":
                await RunPhaseAsync(
                    "Phase 3",
                    ct => new Phase3Runner(store).RunAsync(ct),
                    parsed.CancelAfterMs);
                break;

            case "4":
            case "fire-and-forget":
                await RunPhaseAsync(
                    "Fire-and-Forget",
                    ct => new FireAndForgetRunner(store).RunAsync(ct),
                    parsed.CancelAfterMs);
                break;

            case "5":
            case "phase4":
                await RunPhaseAsync(
                    "Phase 4",
                    ct => new Phase4Runner(store).RunAsync(ct),
                    parsed.CancelAfterMs);
                break;

            case "6":
            case "all":
                var phases = new (string Name, Func<CancellationToken, Task> Run)[]
                {
                    ("Phase 1", ct => new Phase1Runner(store).RunAsync(ct)),
                    ("Phase 2", ct => new Phase2Runner(store).RunAsync(ct)),
                    ("Phase 3", ct => new Phase3Runner(store).RunAsync(ct)),
                    ("Phase 4", ct => new Phase4Runner(store).RunAsync(ct))
                };

                foreach (var phase in phases)
                {
                    bool completed = await RunPhaseAsync(
                        phase.Name,
                        phase.Run,
                        parsed.CancelAfterMs);

                    if (!completed)
                        break;
                }
                break;

            case "0":
                return;

            default:
                Console.WriteLine("Invalid selection.");
                break;
        }
    }

    private static async Task<bool> RunPhaseAsync(
        string phaseName,
        Func<CancellationToken, Task> action,
        int? cancelAfterMs)
    {
        using var cancellationSource = new CancellationTokenSource();

        Interlocked.Exchange(
            ref _activeRunCancellation,
            cancellationSource);

        if (cancelAfterMs.HasValue)
        {
            cancellationSource.CancelAfter(cancelAfterMs.Value);

            Console.WriteLine(
                $"Automatic cancellation scheduled after {cancelAfterMs.Value} ms.");
        }

        try
        {
            await action(cancellationSource.Token);
            return true;
        }
        catch (OperationCanceledException)
            when (cancellationSource.IsCancellationRequested)
        {
            Console.WriteLine($"{phaseName} cancelled.");
            return false;
        }
        finally
        {
            Interlocked.CompareExchange(
                ref _activeRunCancellation,
                null,
                cancellationSource);
        }
    }

    private static (
        string? Mode,
        int? CancelAfterMs,
        string? Error) ParseArgs(string[] args)
    {
        string? mode = null;
        int? cancelAfterMs = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(
                "--cancel-after",
                StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length ||
                    !int.TryParse(args[i + 1], out int milliseconds) ||
                    milliseconds <= 0)
                {
                    return (
                        null,
                        null,
                        "Invalid --cancel-after value. Use a positive number of milliseconds.");
                }

                cancelAfterMs = milliseconds;
                i++;
            }
            else if (args[i].StartsWith("--"))
            {
                return (null, null, $"Unknown option: {args[i]}");
            }
            else if (mode is null)
            {
                mode = args[i].ToLowerInvariant();
            }
            else
            {
                return (
                    null,
                    null,
                    "Only one run mode is allowed.");
            }
        }

        return (mode, cancelAfterMs, null);
    }
}