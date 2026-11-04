using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class SyncProductRepository
{
    private readonly JsonFileStore _store;

    public SyncProductRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Product? GetById(int id)
    {
        Thread.Sleep(200);
        Thread.Sleep(Random.Shared.Next(300, 601));

        var products = _store.Read<Product>(DataFiles.Products);

        return products.FirstOrDefault(product => product.Id == id);
    }
}