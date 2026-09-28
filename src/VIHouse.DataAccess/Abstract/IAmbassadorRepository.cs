using VIHouse.Entities.Referrals;

namespace VIHouse.DataAccess.Abstract;

public interface IAmbassadorRepository : IRepository<Ambassador>
{
    Task<Ambassador?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Ambassador?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Ambassador?> GetByInviteTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<Ambassador?> GetPendingByInviteEmailAsync(string email, CancellationToken ct = default);
}
