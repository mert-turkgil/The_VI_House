using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IPromoCodeRepository : IRepository<PromoCode>
{
    Task<PromoCode?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Atomically increments RedemptionCount only if MaxRedemptions has not yet been reached — same no-oversell pattern as ticket inventory.</summary>
    Task<bool> TryRedeemAsync(Guid promoCodeId, CancellationToken ct = default);
}
