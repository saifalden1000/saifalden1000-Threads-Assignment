using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class CustomerService
{
    private readonly JsonFileStore _store;

    public CustomerService(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<Customer> GetCustomerAsync(
        int customerId,
        CancellationToken ct)
    {
        Log.Info($"[CustomerService] Opening connection for Customer #{customerId}");

        await Task.Delay(200, ct);

        Log.Info($"[CustomerService] Querying Customer #{customerId}");

        await Task.Delay(Random.Shared.Next(300, 601), ct);

        var customers = await _store.ReadAsync<Customer>(
            DataFiles.Customers,
            ct);

        var customer = customers.FirstOrDefault(
            c => c.Id == customerId);

        if (customer is null)
        {
            throw new CustomerNotFoundException(
                $"Customer #{customerId} not found");
        }

        Log.Success($"[CustomerService] Customer #{customerId} found");

        return customer;
    }
}