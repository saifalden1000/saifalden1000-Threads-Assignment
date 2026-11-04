using OrderProcessing.Shared.Logging;
using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase3;

public sealed class PaymentGateway : IPaymentGateway
{
    private readonly JsonFileStore _store;
    private readonly HashSet<int> _timeoutOrderIds;
    private readonly HashSet<int> _declineOrderIds;

    public PaymentGateway(
        JsonFileStore store,
        IEnumerable<int> timeoutOrderIds,
        IEnumerable<int> declineOrderIds)
    {
        _store = store;
        _timeoutOrderIds = timeoutOrderIds.ToHashSet();
        _declineOrderIds = declineOrderIds.ToHashSet();
    }

    public async Task<PaymentResult> ProcessPaymentAsync(
        int orderId,
        decimal amount,
        CancellationToken ct)
    {
        Log.Info($"[PaymentGateway] Authorizing payment for Order #{orderId}");

        await Task.Delay(800, ct);

        Log.Info($"[PaymentGateway] Running fraud check for Order #{orderId}");

        await Task.Delay(500, ct);

        if (_declineOrderIds.Contains(orderId))
        {
            Log.Error($"[PaymentGateway] Payment declined for Order #{orderId}");

            var declinedResult = new PaymentResult
            {
                Success = false,
                TransactionId = null,
                Status = PaymentStatus.Declined,
                Message = "Card declined by issuer"
            };

            await _store.UpdateAsync<PaymentTransaction>(
                DataFiles.Payments,
                payments =>
                {
                    payments.Add(new PaymentTransaction
                    {
                        OrderId = orderId,
                        Amount = amount,
                        Status = PaymentStatus.Declined,
                        TransactionId = null,
                        CreatedAt = DateTime.Now
                    });

                    return payments;
                },
                ct);

            return declinedResult;
        }

        Log.Info($"[PaymentGateway] Capturing payment for Order #{orderId}");

        int captureDelay = _timeoutOrderIds.Contains(orderId) ? 5000 : 700;

        await Task.Delay(captureDelay, ct);

        string transactionId =
            "TXN-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        await _store.UpdateAsync<PaymentTransaction>(
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
            },
            ct);

        Log.Success(
            $"[PaymentGateway] Payment captured for Order #{orderId}: {transactionId}");

        return new PaymentResult
        {
            Success = true,
            TransactionId = transactionId,
            Status = PaymentStatus.Captured,
            Message = null
        };
    }

    public async Task RefundAsync(
        string transactionId,
        CancellationToken ct)
    {
        Log.Info($"[PaymentGateway] Connecting to refund service for {transactionId}");

        await Task.Delay(150, ct);

        Log.Info($"[PaymentGateway] Sending refund request for {transactionId}");

        await Task.Delay(300, ct);

        await Task.Delay(Random.Shared.Next(500, 1501), ct);

        await _store.UpdateAsync<PaymentTransaction>(
            DataFiles.Payments,
            payments =>
            {
                var payment = payments.FirstOrDefault(
                    p => p.TransactionId == transactionId);

                if (payment is not null)
                    payment.Status = PaymentStatus.Refunded;

                return payments;
            },
            ct);

        Log.Success($"[PaymentGateway] Refunded transaction {transactionId}");
    }
}