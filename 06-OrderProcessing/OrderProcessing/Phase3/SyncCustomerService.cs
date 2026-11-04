using OrderProcessing.Shared.Exceptions;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class SyncCustomerService
{
    private readonly JsonFileStore _store;

    public SyncCustomerService(JsonFileStore store)
    {
        _store = store;
    }

    public Customer GetCustomer(int customerId)
    {
        Thread.Sleep(200);
        Thread.Sleep(Random.Shared.Next(300, 601));

        var customers = _store.Read<Customer>(DataFiles.Customers);

        var customer = customers.FirstOrDefault(
            c => c.Id == customerId);

        return customer ?? throw new CustomerNotFoundException(
            $"Customer #{customerId} not found");
    }
}