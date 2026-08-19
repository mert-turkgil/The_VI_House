using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Journal;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfJournalPostRepository(VIHouseDbContext db) : EfRepository<JournalPost>(db), IJournalPostRepository
{
    public Task<JournalPost?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default)
    {
        var query = Set.AsQueryable();

        if (filter.Category is not null)
            query = query.Where(p => p.Category == filter.Category);

        return await query
            // Published is enforced unconditionally, not just when a category filter is present —
            // same defense-in-depth as EfExperienceRepository's unconditional Draft exclusion.
            .Where(p => p.Status == JournalPostStatus.Published)
            .OrderByDescending(p => p.PublishedAt)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);
    }
}
