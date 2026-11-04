using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class SyncCustomerRepository
{
    private readonly JsonFileStore _store;

    public SyncCustomerRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Customer? GetById(int id)
    {
        Thread.Sleep(200);
        Thread.Sleep(Random.Shared.Next(300, 601));

        var customers = _store.Read<Customer>(DataFiles.Customers);

        return customers.FirstOrDefault(customer => customer.Id == id);
    }
}