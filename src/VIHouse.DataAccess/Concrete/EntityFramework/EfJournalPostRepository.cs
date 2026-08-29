using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Journal;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfJournalPostRepository(VIHouseDbContext db) : EfRepository<JournalPost>(db), IJournalPostRepository
{
    public Task<JournalPost?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        Set.Include(p => p.Translations).FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public async Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Include(p => p.Translations).AsQueryable();

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

    public async Task<List<JournalPost>> GetAllWithTranslationsAsync(CancellationToken ct = default) =>
        await Set.Include(p => p.Translations)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public Task<JournalPost?> GetWithDetailAsync(Guid id, CancellationToken ct = default) =>
        Set.Include(p => p.Translations)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<JournalPostMedia?> GetMediaAsync(Guid mediaId, CancellationToken ct = default) =>
        Db.JournalPostMedia.AsNoTracking().FirstOrDefaultAsync(m => m.Id == mediaId, ct);

    public async Task<List<JournalPost>> SearchPublishedAsync(string term, CancellationToken ct = default)
    {
        // Collation forced for the same reason SearchController already forces it on experiences:
        // the database is Turkish-collated, where the dotless-i rules make a default comparison miss
        // "izmir" against a stored "Izmir".
        const string ci = "Latin1_General_CI_AS";

        return await Set.AsNoTracking()
            .Include(p => p.Translations)
            .Where(p => p.Status == JournalPostStatus.Published
                && p.Translations.Any(t =>
                    EF.Functions.Collate(t.Title, ci).Contains(term)
                    || (t.Excerpt != null && EF.Functions.Collate(t.Excerpt, ci).Contains(term))))
            .ToListAsync(ct);
    }
}
