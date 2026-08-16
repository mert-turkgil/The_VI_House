using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfPromoCodeRepository(VIHouseDbContext db) : EfRepository<PromoCode>(db), IPromoCodeRepository
{
    public Task<PromoCode?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.Code == code, ct);

    public async Task<bool> TryRedeemAsync(Guid promoCodeId, CancellationToken ct = default)
    {
        var affected = await Set
            .Where(p => p.Id == promoCodeId && p.IsActive
                        && (p.MaxRedemptions == null || p.RedemptionCount < p.MaxRedemptions))
            .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.RedemptionCount, p => p.RedemptionCount + 1), ct);

        return affected > 0;
    }
}
