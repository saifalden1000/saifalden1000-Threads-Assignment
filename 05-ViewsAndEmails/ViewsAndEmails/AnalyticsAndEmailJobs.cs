
namespace ViewsAndEmails;

public sealed class AnalyticsAndEmailJobs
{
    public readonly Dictionary<string, int> Views = new();
    public readonly Queue<string> EmailJobs = new();
    public readonly List<string> SentEmails = new();

    readonly object _viewsSync = new();
    readonly object _queueSync = new();
    readonly object _sentSync = new();

    public volatile bool ProducersDone;

    public void RecordView(string product)
    {
        lock (_viewsSync)
        {
            if (!Views.ContainsKey(product))
                Views.Add(product, 0);

            Views[product]++;
        }
    }

    public void PlaceOrder(int orderId)
    {
        lock (_queueSync)
        {
            EmailJobs.Enqueue($"Email: order {orderId}");
        }
    }

    public void EmailWorker()
    {
        while (true)
        {
            string? job;

            lock (_queueSync)
            {
                if (EmailJobs.Count > 0)
                    job = EmailJobs.Dequeue();
                else if (ProducersDone)
                    return;
                else
                    job = null;
            }

            if (job is null)
            {
                Thread.Sleep(1);
                continue;
            }

            lock (_sentSync)
            {
                SentEmails.Add(job);
            }
        }
    }
}
