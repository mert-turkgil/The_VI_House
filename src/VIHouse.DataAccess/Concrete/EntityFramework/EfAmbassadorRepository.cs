using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Referrals;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfAmbassadorRepository(VIHouseDbContext db) : EfRepository<Ambassador>(db), IAmbassadorRepository
{
    public Task<Ambassador?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(a => a.Code == code, ct);

    public Task<Ambassador?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(a => a.UserId == userId, ct);

    public Task<Ambassador?> GetByInviteTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(a => a.InviteTokenHash == tokenHash, ct);

    // Emails are compared case-insensitively whatever the database collation is (the site's is Turkish,
    // where I and i are different letters).
    public Task<Ambassador?> GetPendingByInviteEmailAsync(string email, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(a => a.Status == AmbassadorStatus.Pending
            && EF.Functions.Collate(a.InviteEmail!, "Latin1_General_CI_AS") == email, ct);
}
