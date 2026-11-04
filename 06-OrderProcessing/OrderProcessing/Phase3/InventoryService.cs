using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Phase3;

public sealed class InventoryService
{
    private readonly ProductRepository _productRepository;

    public InventoryService(ProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<bool> ReserveStockAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info($"[InventoryService] Reserving inventory for Order #{order.Id}");

        var reservedItems = new List<OrderItem>();

        try
        {
            foreach (var item in order.Items)
            {
                bool reserved = await _productRepository.ReserveStockAsync(
                    item.ProductId,
                    item.Quantity,
                    ct);

                if (!reserved)
                {
                    await ReleaseItemsAsync(reservedItems, ct);

                    Log.Error(
                        $"[InventoryService] Could not reserve all items for Order #{order.Id}");

                    return false;
                }

                reservedItems.Add(item);
            }

            Log.Success(
                $"[InventoryService] All inventory reserved for Order #{order.Id}");

            return true;
        }
        catch (OperationCanceledException)
        {
            await ReleaseItemsAsync(
                reservedItems,
                CancellationToken.None);

            throw;
        }
        catch
        {
            await ReleaseItemsAsync(
                reservedItems,
                CancellationToken.None);

            throw;
        }
    }

    public async Task ReleaseStockAsync(
        Order order,
        CancellationToken ct)
    {
        Log.Info($"[InventoryService] Releasing inventory for Order #{order.Id}");

        await ReleaseItemsAsync(order.Items, ct);

        Log.Success(
            $"[InventoryService] Inventory released for Order #{order.Id}");
    }

    private async Task ReleaseItemsAsync(
        IEnumerable<OrderItem> items,
        CancellationToken ct)
    {
        foreach (var item in items)
        {
            await _productRepository.ReleaseStockAsync(
                item.ProductId,
                item.Quantity,
                ct);
        }
    }
}