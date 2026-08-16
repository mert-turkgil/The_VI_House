using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfExperienceRepository(VIHouseDbContext db) : EfRepository<Experience>(db), IExperienceRepository
{
    public Task<Experience?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(e => e.Slug == slug, ct);

    public Task<Experience?> GetWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Experience?> GetWithDetailsBySlugAsync(string slug, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(e => e.Slug == slug, ct);

    public async Task<List<Experience>> GetPublicListingAsync(ExperienceFilter filter, CancellationToken ct = default)
    {
        var query = Set.Include(e => e.TicketTypes).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(e => e.City == filter.City);
        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(e => e.Country == filter.Country);
        if (filter.Status is not null)
            query = query.Where(e => e.Status == filter.Status);

        return await query
            .Where(e => e.Visibility == ExperienceVisibility.Public)
            .OrderBy(e => e.StartAtUtc)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);
    }

    public Task<List<Experience>> GetUpcomingAsync(int take, CancellationToken ct = default) =>
        Set.Include(e => e.TicketTypes)
            .Where(e => e.Visibility == ExperienceVisibility.Public
                        && e.Status != ExperienceStatus.Completed
                        && e.Status != ExperienceStatus.Draft
                        && e.StartAtUtc > DateTimeOffset.UtcNow)
            .OrderBy(e => e.StartAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task<List<Experience>> GetSignatureAsync(int take, CancellationToken ct = default) =>
        Set.Include(e => e.TicketTypes)
            .Where(e => e.Visibility == ExperienceVisibility.Public && e.IsSignature)
            .OrderBy(e => e.SortOrder)
            .Take(take)
            .ToListAsync(ct);

    private IQueryable<Experience> WithDetails() =>
        Set.Include(e => e.TicketTypes)
            .Include(e => e.ProgramDays).ThenInclude(d => d.Sessions)
            .Include(e => e.Inclusions)
            .Include(e => e.Faqs)
            .Include(e => e.Gallery);
}
