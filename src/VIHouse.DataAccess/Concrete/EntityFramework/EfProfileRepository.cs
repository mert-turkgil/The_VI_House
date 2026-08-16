using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Users;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfProfileRepository(VIHouseDbContext db) : IProfileRepository
{
    public Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public Task AddAsync(Profile profile, CancellationToken ct = default) =>
        db.Profiles.AddAsync(profile, ct).AsTask();

    public void Update(Profile profile) => db.Profiles.Update(profile);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
