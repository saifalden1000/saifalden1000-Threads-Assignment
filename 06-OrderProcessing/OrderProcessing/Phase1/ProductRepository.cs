using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class ProductRepository
{
    private readonly JsonFileStore _store;

    public ProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Product?> GetByIdAsync(
        int id,
        CancellationToken ct)
    {
        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info($"ProductRepository -> Executing query for Product #{id}...");
        await Task.Delay(Random.Shared.Next(300, 601), ct);

        List<Product> products =
            await _store.ReadAsync<Product>(DataFiles.Products, ct);

        Product? product = products.FirstOrDefault(p => p.Id == id);

        if (product is null)
            Log.Error($"ProductRepository -> Product #{id} not found");
        else
            Log.Success($"ProductRepository -> Product #{id} found ({product.Name})");

        return product;
    }

    public async Task<bool> ReserveStockUnsafeAsync(
        int productId,
        int qty,
        CancellationToken ct)
    {
        Log.Info($"ProductRepository -> Opening connection for unsafe reservation #{productId}...");
        await Task.Delay(200, ct);

        List<Product> products =
            await _store.ReadAsync<Product>(DataFiles.Products, ct);

        Product? product = products.FirstOrDefault(p => p.Id == productId);

        if (product is null || product.StockQuantity < qty)
            return false;

        int availableStock = product.StockQuantity;

        Log.Info("ProductRepository -> Beginning unsafe transaction...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Updating stock for Product #{productId}...");
        await Task.Delay(400, ct);

        product.StockQuantity = availableStock - qty;

        await _store.WriteAsync<Product>(
            DataFiles.Products,
            products,
            ct);

        Log.Info("ProductRepository -> Committing unsafe transaction...");
        await Task.Delay(200, ct);

        Log.Success($"ProductRepository -> Unsafe reservation completed for Product #{productId}");

        return true;
    }

    public async Task<bool> ReserveStockAsync(
        int productId,
        int qty,
        CancellationToken ct)
    {
        Log.Info("ProductRepository -> Opening connection...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository -> Beginning transaction...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Updating stock for Product #{productId}...");
        await Task.Delay(400, ct);

        bool reserved = false;

        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                Product? product =
                    products.FirstOrDefault(p => p.Id == productId);

                if (product is null || product.StockQuantity < qty)
                    return products;

                product.StockQuantity -= qty;
                reserved = true;

                return products;
            },
            ct);

        Log.Info("ProductRepository -> Committing transaction...");
        await Task.Delay(200, ct);

        if (reserved)
            Log.Success($"ProductRepository -> Stock reserved for Product #{productId}");
        else
            Log.Error($"ProductRepository -> Could not reserve stock for Product #{productId}");

        return reserved;
    }

    public async Task ReleaseStockAsync(
        int productId,
        int qty,
        CancellationToken ct)
    {
        Log.Info("ProductRepository -> Opening connection for stock release...");
        await Task.Delay(200, ct);

        Log.Info("ProductRepository -> Beginning release transaction...");
        await Task.Delay(100, ct);

        Log.Info($"ProductRepository -> Releasing stock for Product #{productId}...");
        await Task.Delay(400, ct);

        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                Product? product =
                    products.FirstOrDefault(p => p.Id == productId);

                if (product is not null)
                    product.StockQuantity += qty;

                return products;
            },
            ct);

        Log.Info("ProductRepository -> Committing release transaction...");
        await Task.Delay(200, ct);

        Log.Success($"ProductRepository -> Stock released for Product #{productId}");
    }
}