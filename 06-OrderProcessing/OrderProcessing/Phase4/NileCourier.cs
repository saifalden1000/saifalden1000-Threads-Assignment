using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class NileCourier : IShippingCarrier
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failForOrderIds;

    public string Name => "NileCourier";

    public NileCourier(
        JsonFileStore store,
        IEnumerable<int>? failForOrderIds = null)
    {
        _store = store;
        _failForOrderIds = (failForOrderIds ?? []).ToHashSet();
    }

    public async Task<decimal> GetQuoteAsync(
        Order order,
        CancellationToken ct)
    {
        try
        {
            Log.Info("[NileCourier] Starting quote request");

            await Task.Delay(150, ct);

            if (_failForOrderIds.Contains(order.Id))
            {
                throw new ShippingProviderUnavailableException(
                    "NileCourier is unavailable");
            }

            await Task.Delay(Random.Shared.Next(800, 1201), ct);

            var products = await _store.ReadAsync<Product>(
                DataFiles.Products,
                ct);

            int totalQuantity = 0;
            decimal totalWeight = 0;

            foreach (var item in order.Items)
            {
                var product = products.FirstOrDefault(
                    p => p.Id == item.ProductId);

                if (product is null)
                {
                    throw new InvalidOperationException(
                        $"Product #{item.ProductId} not found");
                }

                totalQuantity += item.Quantity;
                totalWeight += product.WeightKg * item.Quantity;
            }

            decimal basePrice = order.SubTotal >= 1000m
                ? 0m
                : 5m + (1.50m * totalQuantity) + (2m * totalWeight);

            decimal price = Math.Round(
                basePrice * 1.10m,
                2,
                MidpointRounding.AwayFromZero);

            Log.Success($"[NileCourier] Quote received: {price:F2}");

            return price;
        }
        catch (OperationCanceledException)
        {
            Log.Info("[NileCourier] cancelled");
            throw;
        }
    }
}