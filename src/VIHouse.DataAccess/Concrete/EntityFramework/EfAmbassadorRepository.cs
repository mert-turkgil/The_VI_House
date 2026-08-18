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
}
