namespace OrderProcessing.Phase2;

public sealed class InvoiceGenerator
{
    public async Task GenerateAsync(
        OrderProcessing.Shared.Models.Order order,
        CancellationToken ct)
    {
        string invoiceContent = await Task.Run(() =>
        {
            long checksum = 0;

            for (int i = 0; i < 20_000_000; i++)
            {
                checksum += (long)i * (i % 7);

                if (i % 1_000_000 == 0)
                    ct.ThrowIfCancellationRequested();
            }

            ct.ThrowIfCancellationRequested();

            return $"""
                    Invoice #{order.Id}
                    Customer ID: {order.CustomerId}
                    Subtotal: {order.SubTotal:F2}
                    Discount: {order.Discount:F2}
                    Shipping: {order.ShippingCost:F2}
                    Total: {order.TotalAmount:F2}
                    Checksum: {checksum}
                    """;
        }, ct);

        ct.ThrowIfCancellationRequested();

        string invoiceDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "invoices");

        Directory.CreateDirectory(invoiceDirectory);

        string invoicePath = Path.Combine(
            invoiceDirectory,
            $"invoice_{order.Id}.txt");

        await File.WriteAllTextAsync(
            invoicePath,
            invoiceContent,
            ct);
    }
}