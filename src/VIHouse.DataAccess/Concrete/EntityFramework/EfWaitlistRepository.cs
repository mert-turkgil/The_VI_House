using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfWaitlistRepository(VIHouseDbContext db) : EfRepository<WaitlistEntry>(db), IWaitlistRepository
{
    public Task<List<WaitlistEntry>> GetByExperienceOrderedAsync(Guid experienceId, CancellationToken ct = default) =>
        Set.Where(w => w.ExperienceId == experienceId).OrderBy(w => w.Position).ToListAsync(ct);

    public async Task<int> GetNextPositionAsync(Guid experienceId, CancellationToken ct = default)
    {
        var max = await Set.Where(w => w.ExperienceId == experienceId)
            .Select(w => (int?)w.Position)
            .MaxAsync(ct);

        return (max ?? 0) + 1;
    }
}
