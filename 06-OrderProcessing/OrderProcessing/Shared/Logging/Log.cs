namespace OrderProcessing.Shared.Logging;

public static class Log
{
    private static readonly object Sync = new();
    private static readonly HashSet<int> ThreadIds = [];

    public static void ResetThreadTracking()
    {
        lock (Sync)
        {
            ThreadIds.Clear();
        }
    }

    public static int DistinctThreadCount
    {
        get
        {
            lock (Sync)
            {
                return ThreadIds.Count;
            }
        }
    }

    public static void Info(string message)
    {
        Write(message, ConsoleColor.Gray);
    }

    public static void Success(string message)
    {
        Write(message, ConsoleColor.Green);
    }

    public static void Error(string message)
    {
        Write(message, ConsoleColor.Red);
    }

    private static void Write(string message, ConsoleColor color)
    {
        lock (Sync)
        {
            int threadId = Environment.CurrentManagedThreadId;
            ThreadIds.Add(threadId);

            ConsoleColor original = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = color;

                Console.WriteLine(
                    $"[{DateTime.Now:HH:mm:ss.fff}] " +
                    $"[Thread {threadId}] {message}");
            }
            finally
            {
                Console.ForegroundColor = original;
            }
        }
    }
}