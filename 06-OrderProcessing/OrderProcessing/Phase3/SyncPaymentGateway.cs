using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class SyncPaymentGateway
{
    private readonly JsonFileStore _store;

    public SyncPaymentGateway(JsonFileStore store)
    {
        _store = store;
    }

    public PaymentResult ProcessPayment(int orderId, decimal amount)
    {
        Thread.Sleep(800);
        Thread.Sleep(500);
        Thread.Sleep(700);

        string transactionId =
            "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        _store.Update<PaymentTransaction>(
            DataFiles.Payments,
            payments =>
            {
                payments.Add(new PaymentTransaction
                {
                    OrderId = orderId,
                    Amount = amount,
                    Status = PaymentStatus.Captured,
                    TransactionId = transactionId,
                    CreatedAt = DateTime.Now
                });

                return payments;
            });

        return new PaymentResult
        {
            Success = true,
            TransactionId = transactionId,
            Status = PaymentStatus.Captured,
            Message = null
        };
    }
}