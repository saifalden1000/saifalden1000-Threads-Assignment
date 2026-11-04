using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class SyncInventoryService
{
    private readonly JsonFileStore _store;

    public SyncInventoryService(JsonFileStore store)
    {
        _store = store;
    }

    public bool ReserveStock(Order order)
    {
        var reservedItems = new List<OrderItem>();

        foreach (var item in order.Items)
        {
            Thread.Sleep(200);
            Thread.Sleep(100);
            Thread.Sleep(400);

            bool reserved = false;

            _store.Update<Product>(
                DataFiles.Products,
                products =>
                {
                    var product = products.FirstOrDefault(
                        p => p.Id == item.ProductId);

                    if (product is null || product.StockQuantity < item.Quantity)
                        return products;

                    product.StockQuantity -= item.Quantity;
                    reserved = true;

                    return products;
                });

            Thread.Sleep(200);

            if (!reserved)
            {
                ReleaseItems(reservedItems);
                Log.Error($"[SyncInventoryService] Could not reserve Product #{item.ProductId}");
                return false;
            }

            reservedItems.Add(item);
        }

        return true;
    }

    public void ReleaseStock(Order order)
    {
        ReleaseItems(order.Items);
    }

    private void ReleaseItems(IEnumerable<OrderItem> items)
    {
        foreach (var item in items)
        {
            Thread.Sleep(200);
            Thread.Sleep(100);
            Thread.Sleep(400);

            _store.Update<Product>(
                DataFiles.Products,
                products =>
                {
                    var product = products.FirstOrDefault(
                        p => p.Id == item.ProductId);

                    if (product is not null)
                        product.StockQuantity += item.Quantity;

                    return products;
                });

            Thread.Sleep(200);
        }
    }
}