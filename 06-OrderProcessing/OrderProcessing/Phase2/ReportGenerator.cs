using System.Text;
using System.Text.Json;
using OrderProcessing.Phase1;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase2;

public sealed class ReportGenerator
{
    private readonly OrderRepository _orderRepository;
    private readonly ProductRepository _productRepository;

    public ReportGenerator(
        OrderRepository orderRepository,
        ProductRepository productRepository)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
    }

    public async Task<Report> GenerateAsync(CancellationToken ct)
    {
        var orders = await _orderRepository.GetAllAsync(ct);

        var completedOrders = orders
            .Where(o => o.Status == OrderStatus.Completed)
            .ToList();

        decimal totalRevenue = completedOrders.Sum(o => o.TotalAmount);

        var productQuantities = completedOrders
            .SelectMany(o => o.Items)
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => item.Quantity)
            })
            .ToList();

        var productSales = new List<(int ProductId, string Name, int Quantity)>();

        foreach (var item in productQuantities)
        {
            var product = await _productRepository.GetByIdAsync(
                item.ProductId,
                ct);

            if (product is not null)
            {
                productSales.Add((
                    product.Id,
                    product.Name,
                    item.Quantity));
            }
        }

        var topProduct = productSales
            .OrderByDescending(p => p.Quantity)
            .ThenBy(p => p.ProductId)
            .FirstOrDefault();

        var report = new Report
        {
            TotalOrders = orders.Count,
            TotalRevenue = totalRevenue,
            CompletedCount = orders.Count(o => o.Status == OrderStatus.Completed),
            RejectedCount = orders.Count(o => o.Status == OrderStatus.Rejected),
            FailedCount = orders.Count(o => o.Status == OrderStatus.Failed),
            PaymentFailedCount = orders.Count(o => o.Status == OrderStatus.PaymentFailed),
            TopSellingProduct = topProduct.Name ?? string.Empty,
            TopSellingQuantity = topProduct.Quantity,
            AverageOrderValue = completedOrders.Count == 0
                ? 0
                : Math.Round(
                    totalRevenue / completedOrders.Count,
                    2,
                    MidpointRounding.AwayFromZero),
            GeneratedAt = DateTime.Now
        };

        string reportDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "Data",
            "reports");

        Directory.CreateDirectory(reportDirectory);

        string fileName = $"report_{DateTime.Now:yyyyMMdd_HHmmss}.json";
        string filePath = Path.Combine(reportDirectory, fileName);

        string json = JsonSerializer.Serialize(
            report,
            JsonFileStore.JsonOptions);

        await File.WriteAllTextAsync(
            filePath,
            json,
            Encoding.UTF8,
            ct);

        return report;
    }
}