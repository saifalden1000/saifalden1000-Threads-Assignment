using OrderProcessing.Phase1;
using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase4;

public sealed class ShippingQuoteDemo
{
    private readonly JsonFileStore _store;

    public ShippingQuoteDemo(JsonFileStore store)
    {
        _store = store;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var seeder = new DataSeeder(_store);
        await seeder.ResetAsync(ct);

        var incomingOrderRepository = new IncomingOrderRepository(_store);

        var orders = await incomingOrderRepository.GetAllAsync(ct);

        var products = await _store.ReadAsync<Product>(
            DataFiles.Products,
            ct);

        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                var product = products.FirstOrDefault(
                    p => p.Id == item.ProductId);

                if (product is null)
                {
                    throw new InvalidOperationException(
                        $"Product #{item.ProductId} not found");
                }

                item.UnitPrice = product.Price;
            }

            order.SubTotal = order.Items.Sum(
                item => item.UnitPrice * item.Quantity);
        }

        var carriers = new IShippingCarrier[]
        {
            new FastShip(_store),
            new CairoExpress(_store),
            new NileCourier(_store)
        };

        var quoteService = new ShippingQuoteService(carriers);

        Log.Info("========== SHIPPING QUOTE RACE ==========");

        foreach (int orderId in Enumerable.Range(1, 5))
        {
            ct.ThrowIfCancellationRequested();

            var order = orders.First(o => o.Id == orderId);

            try
            {
                var quote = await quoteService.GetFastestQuoteAsync(
                    order,
                    ct);

                Log.Success(
                    $"Order #{order.Id} -> {quote.Carrier}, " +
                    $"Price: {quote.Price:F2}, " +
                    $"Elapsed: {quote.ElapsedMs} ms");

                if (quote.CancelledCarriers.Count > 0)
                {
                    Log.Info(
                        $"Cancelled carriers: " +
                        $"{string.Join(", ", quote.CancelledCarriers)}");
                }
            }
            catch (ShippingProviderUnavailableException ex)
            {
                Log.Error($"Order #{order.Id}: {ex.Message}");
            }
        }

        Log.Info("========== ALL CARRIERS FAILURE TEST ==========");

        var order6 = orders.First(o => o.Id == 6);

        var failingCarriers = new IShippingCarrier[]
        {
            new FastShip(_store, new[] { 6 }),
            new CairoExpress(_store, new[] { 6 }),
            new NileCourier(_store, new[] { 6 })
        };

        var failingQuoteService = new ShippingQuoteService(
            failingCarriers);

        try
        {
            await failingQuoteService.GetFastestQuoteAsync(
                order6,
                ct);
        }
        catch (ShippingProviderUnavailableException ex)
        {
            Log.Error(
                $"Order #{order6.Id}: Expected result - {ex.Message}");
        }
    }
}