using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class OrderRepository
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _failSaveForOrderIds;

    public OrderRepository(
        JsonFileStore store,
        IEnumerable<int>? failSaveForOrderIds = null)
    {
        _store = store;
        _failSaveForOrderIds = (failSaveForOrderIds ?? []).ToHashSet();
    }

    public async Task SaveAsync(Order order, CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Beginning transaction for Order #{order.Id}...");
        await Task.Delay(100, ct);

        Log.Info($"OrderRepository -> Inserting/Updating Order #{order.Id}...");
        await Task.Delay(400, ct);

        await _store.UpdateAsync<Order>(
            DataFiles.Orders,
            orders =>
            {
                int index = orders.FindIndex(o => o.Id == order.Id);

                if (index >= 0)
                    orders[index] = order;
                else
                    orders.Add(order);

                return orders;
            },
            ct);

        Log.Info($"OrderRepository -> Committing Order #{order.Id}...");
        await Task.Delay(200, ct);

        if (order.Status == OrderStatus.Completed &&
            _failSaveForOrderIds.Contains(order.Id))
        {
            throw new DatabaseWriteException(
                "Connection lost during commit");
        }

        Log.Success($"OrderRepository -> Order #{order.Id} saved");
    }

    public void Save(Order order)
    {
        Thread.Sleep(200);
        Thread.Sleep(100);
        Thread.Sleep(400);

        _store.Update<Order>(
            DataFiles.Orders,
            orders =>
            {
                int index = orders.FindIndex(o => o.Id == order.Id);

                if (index >= 0)
                    orders[index] = order;
                else
                    orders.Add(order);

                return orders;
            });

        Thread.Sleep(200);

        Log.Success(
            $"OrderRepository -> Order #{order.Id} saved synchronously");
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("OrderRepository -> Executing query for all orders...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Order> orders =
            await _store.ReadAsync<Order>(DataFiles.Orders, ct);

        Log.Success($"OrderRepository -> Retrieved {orders.Count} orders");

        return orders;
    }

    public async Task<List<Order>> GetByStatusAsync(
        OrderStatus status,
        CancellationToken ct)
    {
        Log.Info("OrderRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"OrderRepository -> Executing query for status {status}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Order> orders =
            await _store.ReadAsync<Order>(DataFiles.Orders, ct);

        List<Order> filtered = orders
            .Where(o => o.Status == status)
            .ToList();

        Log.Success(
            $"OrderRepository -> Retrieved {filtered.Count} orders with status {status}");

        return filtered;
    }
}