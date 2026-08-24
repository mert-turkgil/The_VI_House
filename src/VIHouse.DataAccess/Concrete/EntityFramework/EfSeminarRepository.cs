using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfSeminarRepository(VIHouseDbContext db) : EfRepository<Seminar>(db), ISeminarRepository
{
    public Task<Seminar?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        WithDetail().FirstOrDefaultAsync(s => s.Slug == slug, ct);

    public Task<Seminar?> GetWithDetailAsync(Guid id, CancellationToken ct = default) =>
        WithDetail().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Seminar?> GetByMediaIdAsync(Guid mediaId, CancellationToken ct = default) =>
        WithDetail().FirstOrDefaultAsync(s => s.Media.Any(m => m.Id == mediaId), ct);

    public async Task<List<Seminar>> GetPublicListingAsync(SeminarFilter filter, CancellationToken ct = default)
    {
        // Translations only: the listing renders a title and a summary, never a body or a gallery,
        // and pulling media for every card would be a second collection join for nothing.
        var query = Set
            .Include(s => s.Translations)
            .Where(s => s.Status == SeminarStatus.Published);

        // Unlisted is excluded from every listing by definition — reachable by direct link only.
        query = filter.IncludeMembersOnly
            ? query.Where(s => s.Visibility == SeminarVisibility.Public || s.Visibility == SeminarVisibility.Members)
            : query.Where(s => s.Visibility == SeminarVisibility.Public);

        if (filter.UpcomingOnly)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(s => s.StartAtUtc == null || s.StartAtUtc >= now);
        }

        return await query
            .OrderBy(s => s.SortOrder)
            .ThenByDescending(s => s.PublishedAt)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public async Task<List<Seminar>> GetAllForAdminAsync(CancellationToken ct = default) =>
        await Set
            .Include(s => s.Translations)
            .OrderByDescending(s => s.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<List<Seminar>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        return await Set
            .Include(s => s.Translations)
            .Where(s => ids.Contains(s.Id))
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    public Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken ct = default) =>
        Set.AnyAsync(s => s.Slug == slug && (exceptId == null || s.Id != exceptId), ct);

    /// <summary>
    /// AsSplitQuery because a seminar with a dozen media rows and four translations would otherwise
    /// come back as a cartesian product of the two collections — the classic EF Core "one row per
    /// combination" blow-up, and the reason the same seminar's body would arrive repeated 12 times.
    /// </summary>
    private IQueryable<Seminar> WithDetail() =>
        Set.Include(s => s.Translations)
           .Include(s => s.Media)
           .AsSplitQuery();
}
