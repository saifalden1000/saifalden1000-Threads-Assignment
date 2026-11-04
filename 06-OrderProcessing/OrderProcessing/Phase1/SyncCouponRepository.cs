using OrderProcessing.Shared.Models;
using OrderProcessing.Shared.Storage;

namespace OrderProcessing.Phase1;

public sealed class SyncCouponRepository
{
    private readonly JsonFileStore _store;

    public SyncCouponRepository(JsonFileStore store)
    {
        _store = store;
    }

    public Coupon? GetByCode(string code)
    {
        Thread.Sleep(200);
        Thread.Sleep(Random.Shared.Next(300, 601));

        var coupons = _store.Read<Coupon>(DataFiles.Coupons);

        return coupons.FirstOrDefault(
            coupon => string.Equals(
                coupon.Code,
                code,
                StringComparison.OrdinalIgnoreCase));
    }
}