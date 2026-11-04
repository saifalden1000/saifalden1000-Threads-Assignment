
using OrderProcessing.Shared.Models;

namespace OrderProcessing.Shared.Storage;

public sealed class DataSeeder
{
    private readonly JsonFileStore _store;
    private readonly string _dataDirectory;

    public DataSeeder(JsonFileStore store)
    {
        _store = store;
        _dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
    }

    public async Task ResetAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (Directory.Exists(_dataDirectory))
            Directory.Delete(_dataDirectory, true);

        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(
            Path.Combine(_dataDirectory, DataFiles.Invoices));
        Directory.CreateDirectory(
            Path.Combine(_dataDirectory, DataFiles.Reports));

        var customers = new List<Customer>
        {
            new() { Id = 1, Name = "Ahmed Ali", Email = "ahmed.ali@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
            new() { Id = 2, Name = "Sara Mohamed", Email = "sara.mohamed@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
            new() { Id = 3, Name = "Omar Hassan", Email = "omar.hassan@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
            new() { Id = 4, Name = "Mona Ibrahim", Email = "mona.ibrahim@example.com", IsActive = false, LoyaltyTier = LoyaltyTier.Silver },
            new() { Id = 5, Name = "Youssef Adel", Email = "youssef.adel@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
            new() { Id = 6, Name = "Nour Khaled", Email = "nour.khaled@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
            new() { Id = 7, Name = "Karim Samir", Email = "karim.samir@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver },
            new() { Id = 8, Name = "Laila Mahmoud", Email = "laila.mahmoud@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Gold },
            new() { Id = 9, Name = "Hassan Tarek", Email = "hassan.tarek@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Bronze },
            new() { Id = 10, Name = "Dina Fathy", Email = "dina.fathy@example.com", IsActive = true, LoyaltyTier = LoyaltyTier.Silver }
        };

        var products = new List<Product>
        {
            new() { Id = 1, Name = "Laptop", Price = 1200.00m, StockQuantity = 10, WeightKg = 2.2m },
            new() { Id = 2, Name = "Smartphone", Price = 800.00m, StockQuantity = 15, WeightKg = 0.4m },
            new() { Id = 3, Name = "Wireless Mouse", Price = 25.00m, StockQuantity = 2, WeightKg = 0.1m },
            new() { Id = 4, Name = "Mechanical Keyboard", Price = 90.00m, StockQuantity = 1, WeightKg = 0.9m },
            new() { Id = 5, Name = "USB-C Hub", Price = 45.00m, StockQuantity = 30, WeightKg = 0.2m },
            new() { Id = 6, Name = "Headphones", Price = 150.00m, StockQuantity = 12, WeightKg = 0.3m },
            new() { Id = 7, Name = "27\" Monitor", Price = 350.00m, StockQuantity = 6, WeightKg = 5.0m },
            new() { Id = 8, Name = "Webcam", Price = 70.00m, StockQuantity = 20, WeightKg = 0.2m }
        };

        DateTime today = DateTime.Today;

        var coupons = new List<Coupon>
        {
            new() { Code = "SAVE10", DiscountPercent = 10, ExpiryDate = today.AddDays(180) },
            new() { Code = "WELCOME15", DiscountPercent = 15, ExpiryDate = today.AddDays(90) },
            new() { Code = "FLASH20", DiscountPercent = 20, ExpiryDate = today.AddDays(7) },
            new() { Code = "OLD20", DiscountPercent = 20, ExpiryDate = today.AddDays(-30) }
        };

        DateTime createdAt = DateTime.Now;

        var incomingOrders = new List<Order>
        {
            CreateOrder(1, 1, "SAVE10", createdAt, (1, 1), (3, 1)),
            CreateOrder(2, 2, null, createdAt, (2, 1)),
            CreateOrder(3, 3, "WELCOME15", createdAt, (5, 2), (8, 1)),
            CreateOrder(4, 4, null, createdAt, (6, 1)),
            CreateOrder(5, 5, null, createdAt, (7, 1)),
            CreateOrder(6, 6, "FLASH20", createdAt, (5, 1)),
            CreateOrder(7, 7, null, createdAt, (6, 1), (8, 1)),
            CreateOrder(8, 8, "SAVE10", createdAt, (1, 1)),
            CreateOrder(9, 9, null, createdAt, (3, 5)),
            CreateOrder(10, 10, null, createdAt, (4, 1)),
            CreateOrder(11, 1, null, createdAt, (4, 1)),
            CreateOrder(12, 2, "OLD20", createdAt, (2, 1)),
            CreateOrder(13, 3, null, createdAt, (6, 2)),
            CreateOrder(14, 5, "WELCOME15", createdAt, (8, 2), (5, 1)),
            CreateOrder(15, 99, null, createdAt, (2, 1)),
            CreateOrder(16, 6, null, createdAt, (7, 1), (5, 1)),
            CreateOrder(17, 7, "SAVE10", createdAt, (2, 1)),
            CreateOrder(18, 8, "FAKE50", createdAt, (6, 1)),
            CreateOrder(19, 9, null, createdAt, (8, 1)),
            CreateOrder(20, 10, "FLASH20", createdAt, (1, 1), (6, 1))
        };

        var emptyOrders = new List<Order>();
        var emptyPayments = new List<PaymentTransaction>();
        var emptyEmails = new List<EmailMessage>();

        await SeedAsync(DataFiles.Customers, customers, ct);
        await SeedAsync(DataFiles.Products, products, ct);
        await SeedAsync(DataFiles.Coupons, coupons, ct);
        await SeedAsync(DataFiles.IncomingOrders, incomingOrders, ct);
        await SeedAsync(DataFiles.Orders, emptyOrders, ct);
        await SeedAsync(DataFiles.Payments, emptyPayments, ct);
        await SeedAsync(DataFiles.Emails, emptyEmails, ct);
        await SeedAsync(DataFiles.EmailRetry, emptyEmails, ct);
    }

    private async Task SeedAsync<T>(
        string fileName,
        List<T> records,
        CancellationToken ct)
    {
        await _store.WriteAsync(fileName, records, ct);

        Console.WriteLine(
            $"[Seeder] {fileName} -> {records.Count} records");
    }

    private static Order CreateOrder(
        int id,
        int customerId,
        string? couponCode,
        DateTime createdAt,
        params (int ProductId, int Quantity)[] items)
    {
        return new Order
        {
            Id = id,
            CustomerId = customerId,
            CouponCode = couponCode,
            Items = items.Select(item => new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = 0
            }).ToList(),
            SubTotal = 0,
            Discount = 0,
            ShippingCost = 0,
            TotalAmount = 0,
            Status = OrderStatus.Pending,
            CreatedAt = createdAt,
            FailureReason = null,
            TransactionId = null
        };
    }
}
