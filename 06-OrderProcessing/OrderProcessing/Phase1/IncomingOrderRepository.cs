using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class IncomingOrderRepository
{
    private readonly JsonFileStore _store;

    public IncomingOrderRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        Log.Info("[IncomingOrderRepository] Opening incoming orders file");

        await Task.Delay(200, cancellationToken);

        Log.Info("[IncomingOrderRepository] Reading incoming orders");

        await Task.Delay(Random.Shared.Next(300, 601), cancellationToken);

        var orders = await _store.ReadAsync<Order>(
            DataFiles.IncomingOrders,
            cancellationToken);

        Log.Success($"[IncomingOrderRepository] Retrieved {orders.Count} incoming orders");

        return orders;
    }
}