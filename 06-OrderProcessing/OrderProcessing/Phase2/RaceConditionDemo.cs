using OrderProcessing.Phase1;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class RaceConditionDemo
{
    private readonly JsonFileStore _store;
    private readonly ProductRepository _productRepository;

    public RaceConditionDemo(
        JsonFileStore store,
        ProductRepository productRepository)
    {
        _store = store;
        _productRepository = productRepository;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        Console.WriteLine();
        Console.WriteLine("===== Unsafe Stock Reservation Demo =====");

        await SetKeyboardStockAsync(1, ct);

        var unsafeTask1 = _productRepository.ReserveStockUnsafeAsync(4, 1, ct);
        var unsafeTask2 = _productRepository.ReserveStockUnsafeAsync(4, 1, ct);

        bool unsafeResult1 = await unsafeTask1;
        bool unsafeResult2 = await unsafeTask2;

        Product? unsafeProduct = await GetKeyboardAsync(ct);

        Console.WriteLine($"Order #10 reservation: {unsafeResult1}");
        Console.WriteLine($"Order #11 reservation: {unsafeResult2}");
        Console.WriteLine($"Stock after unsafe reservations: {unsafeProduct?.StockQuantity}");

        Console.WriteLine();
        Console.WriteLine("===== Safe Stock Reservation Demo =====");

        await SetKeyboardStockAsync(1, ct);

        var safeTask1 = _productRepository.ReserveStockAsync(4, 1, ct);
        var safeTask2 = _productRepository.ReserveStockAsync(4, 1, ct);

        bool safeResult1 = await safeTask1;
        bool safeResult2 = await safeTask2;

        Product? safeProduct = await GetKeyboardAsync(ct);

        Console.WriteLine($"Order #10 reservation: {safeResult1}");
        Console.WriteLine($"Order #11 reservation: {safeResult2}");
        Console.WriteLine($"Stock after safe reservations: {safeProduct?.StockQuantity}");
    }

    private async Task SetKeyboardStockAsync(
        int quantity,
        CancellationToken ct)
    {
        await _store.UpdateAsync<Product>(
            DataFiles.Products,
            products =>
            {
                Product? keyboard = products.FirstOrDefault(p => p.Id == 4);

                if (keyboard is not null)
                    keyboard.StockQuantity = quantity;

                return products;
            },
            ct);
    }

    private async Task<Product?> GetKeyboardAsync(CancellationToken ct)
    {
        List<Product> products =
            await _store.ReadAsync<Product>(DataFiles.Products, ct);

        return products.FirstOrDefault(p => p.Id == 4);
    }
}