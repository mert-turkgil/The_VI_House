using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfExperienceRepository(VIHouseDbContext db) : EfRepository<Experience>(db), IExperienceRepository
{
    /// <summary>Explicit collation for user-supplied text comparisons — see the note in the city
    /// filter below. Matches the constant SearchController uses for the same reason.</summary>
    private const string CaseInsensitive = "Latin1_General_CI_AS";

    public Task<Experience?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(e => e.Slug == slug, ct);

    public Task<Experience?> GetWithDetailsAsync(Guid id, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Experience?> GetWithDetailsBySlugAsync(string slug, CancellationToken ct = default) =>
        WithDetails().FirstOrDefaultAsync(e => e.Slug == slug, ct);

    public async Task<List<Experience>> GetPublicListingAsync(ExperienceFilter filter, CancellationToken ct = default)
    {
        var query = Set.Include(e => e.TicketTypes).Include(e => e.Translations).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            // Contains + an explicit case-insensitive collation, not ==. The filter value arrives
            // from a query string that anything can put anything into, and an exact match meant
            // "lond", "London " or "london" all returned an empty page. The collation is spelled out
            // for the same reason SearchController does it: the server's default is Turkish, where
            // I/ı casing does not behave the way this comparison needs.
            var city = filter.City.Trim();
            query = query.Where(e => EF.Functions.Collate(e.City, CaseInsensitive).Contains(city));
        }
        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(e => e.Country == filter.Country);
        if (filter.Status is not null)
            query = query.Where(e => e.Status == filter.Status);

        if (!string.IsNullOrWhiteSpace(filter.Topic))
        {
            // A trending chip is a keyword search, not a lookup against a controlled taxonomy —
            // there is no Experience.Topic column, and pretending there is would mean inventing
            // categories that do not match how these rooms are actually described. Matched against
            // the same free-text fields a search box would use, on the experience's own copy and
            // every translation's, so a keyword mentioned only in the Turkish description still
            // surfaces the experience to someone browsing in English. Empty results for a chip are
            // a correct answer — nothing currently listed happens to mention that topic — not a bug.
            var topic = filter.Topic.Trim();
            query = query.Where(e =>
                EF.Functions.Collate(e.Title, CaseInsensitive).Contains(topic) ||
                EF.Functions.Collate(e.Description, CaseInsensitive).Contains(topic) ||
                (e.ShortSummary != null && EF.Functions.Collate(e.ShortSummary, CaseInsensitive).Contains(topic)) ||
                (e.AudienceTags != null && EF.Functions.Collate(e.AudienceTags, CaseInsensitive).Contains(topic)) ||
                e.Translations.Any(t =>
                    EF.Functions.Collate(t.Title, CaseInsensitive).Contains(topic) ||
                    (t.Description != null && EF.Functions.Collate(t.Description, CaseInsensitive).Contains(topic)) ||
                    (t.ShortSummary != null && EF.Functions.Collate(t.ShortSummary, CaseInsensitive).Contains(topic)) ||
                    (t.AudienceTags != null && EF.Functions.Collate(t.AudienceTags, CaseInsensitive).Contains(topic))));
        }

        return await query
            // Draft is excluded unconditionally, not just when the caller asks for a specific
            // status — an admin can set Visibility=Public on a still-Draft experience while
            // preparing it, and that must never be enough on its own to make it publicly listed.
            .Where(e => e.Visibility == ExperienceVisibility.Public && e.Status != ExperienceStatus.Draft)
            .OrderBy(e => e.StartAtUtc)
            .Skip(filter.Skip)
            .Take(filter.Take)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetPublicCitiesAsync(CancellationToken ct = default) =>
        await Set
            // Deliberately the same visibility predicate as GetPublicListingAsync — offering a city
            // in the dropdown that the listing then filters away would be its own dead end.
            .Where(e => e.Visibility == ExperienceVisibility.Public && e.Status != ExperienceStatus.Draft)
            .Select(e => e.City)
            .Distinct()
            .OrderBy(city => city)
            .ToListAsync(ct);

    public Task<List<Experience>> GetUpcomingAsync(int take, CancellationToken ct = default) =>
        Set.Include(e => e.TicketTypes)
            .Include(e => e.Translations)
            .Where(e => e.Visibility == ExperienceVisibility.Public
                        && e.Status != ExperienceStatus.Completed
                        && e.Status != ExperienceStatus.Draft
                        && e.StartAtUtc > DateTimeOffset.UtcNow)
            .OrderBy(e => e.StartAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task<List<Experience>> GetSignatureAsync(int take, CancellationToken ct = default) =>
        Set.Include(e => e.TicketTypes)
            .Include(e => e.Translations)
            .Where(e => e.Visibility == ExperienceVisibility.Public && e.IsSignature && e.Status != ExperienceStatus.Draft)
            .OrderBy(e => e.SortOrder)
            .Take(take)
            .ToListAsync(ct);

    private IQueryable<Experience> WithDetails() =>
        Set.Include(e => e.TicketTypes)
            .Include(e => e.ProgramDays).ThenInclude(d => d.Sessions)
            .Include(e => e.Inclusions)
            .Include(e => e.Faqs)
            .Include(e => e.Gallery)
            // Without this the resolver sees an empty Translations list and silently serves the
            // English columns in every language — a failure that looks exactly like "not translated
            // yet", which is why it is worth stating rather than leaving to be noticed.
            .Include(e => e.Translations)
            .Include(e => e.MembershipAccess);
}
