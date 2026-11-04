namespace OrderProcessing.Phase4;

public sealed class ShippingQuote
{
    public string Carrier { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public long ElapsedMs { get; set; }

    public List<string> CancelledCarriers { get; set; } = [];
}