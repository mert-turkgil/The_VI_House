using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfWaitlistRepository(VIHouseDbContext db) : EfRepository<WaitlistEntry>(db), IWaitlistRepository
{
    public Task<List<WaitlistEntry>> GetByExperienceOrderedAsync(Guid experienceId, CancellationToken ct = default) =>
        Set.Where(w => w.ExperienceId == experienceId)
            // CreatedAt breaks the tie because GetNextPositionAsync is read-then-write: two people
            // joining in the same instant can be handed the same number, and when they are, the one
            // who actually arrived first should still be listed first.
            .OrderBy(w => w.Position).ThenBy(w => w.CreatedAt)
            .ToListAsync(ct);

    public Task<WaitlistEntry?> FindByEmailAsync(Guid experienceId, string email, CancellationToken ct = default)
    {
        // Lowercased on both sides rather than left to the database: this column is compared under
        // the Turkish collation, where "I" and "ı" are separate letters, so an address typed with a
        // capital I would not match the row stored from the same address in lower case.
        var normalised = email.Trim().ToLowerInvariant();
        return Set.FirstOrDefaultAsync(w => w.ExperienceId == experienceId && w.Email == normalised, ct);
    }

    public async Task<int> GetNextPositionAsync(Guid experienceId, CancellationToken ct = default)
    {
        var max = await Set.Where(w => w.ExperienceId == experienceId)
            .Select(w => (int?)w.Position)
            .MaxAsync(ct);

        return (max ?? 0) + 1;
    }
}
