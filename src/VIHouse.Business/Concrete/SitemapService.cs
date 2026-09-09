using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Everything on the public site that is worth a crawler's time, gathered from the same repositories
/// the pages themselves use.
///
/// Built from the real listings rather than a hand-kept list, because the failure mode of a
/// hand-kept list is invisible: nothing errors when it omits half the journal or points at an
/// experience that has since been unpublished, and nobody finds out until traffic does not arrive.
/// </summary>
public class SitemapService(
    IExperienceService experiences,
    IJournalService journal,
    ISeminarService seminars) : ISitemapService
{
    /// <summary>
    /// Pages that exist in code rather than in the database. Every one is fully translated, so all
    /// four languages are declared.
    ///
    /// Deliberately absent: /account, /checkout, /onboarding, /apply, /join, /invitation, /r,
    /// /members and /search. Some are behind a login, some are steps in a funnel that mean nothing
    /// out of order, and /search generates unbounded near-duplicate URLs — the classic way to spend
    /// a site's crawl budget on nothing.
    /// </summary>
    private static readonly (string Path, string Freq, double Priority)[] StaticPages =
    [
        ("/", "weekly", 1.0),
        ("/experiences", "daily", 0.9),
        ("/membership", "weekly", 0.8),
        ("/journal", "daily", 0.8),
        ("/sessions", "weekly", 0.7),
        ("/about", "monthly", 0.6),
        ("/faq", "monthly", 0.5),
        ("/contact", "monthly", 0.5),
        ("/legal/terms", "yearly", 0.2),
        ("/legal/privacy", "yearly", 0.2),
        ("/legal/cookies", "yearly", 0.2),
    ];

    public async Task<IReadOnlyList<SitemapEntry>> GetEntriesAsync(CancellationToken ct = default)
    {
        var all = new List<SitemapEntry>();

        foreach (var (path, freq, priority) in StaticPages)
        {
            all.Add(new SitemapEntry(path, null, freq, priority, SiteCultures.Names)
            {
                Section = "Pages",
            });
        }

        // Take is deliberately generous rather than unbounded: a sitemap has a 50,000-URL ceiling
        // and this site will not approach it, but an unbounded query on a growing table is the kind
        // of thing that is fine until the day it is not.
        var experienceList = await experiences.GetPublicListingAsync(new ExperienceFilter { Take = 1000 }, ct);

        foreach (var e in experienceList.Where(e => e.Status != ExperienceStatus.Draft))
        {
            all.Add(new SitemapEntry(
                $"/experiences/{e.Slug}",
                e.UpdatedAt ?? e.CreatedAt,
                // Something with a fixed date that has passed will not change again; something open
                // for applications changes as places go.
                e.EndAtUtc < DateTimeOffset.UtcNow ? "yearly" : "weekly",
                e.Status == ExperienceStatus.ApplicationsOpen ? 0.8 : 0.6,
                CulturesOf(e))
            {
                Title = e.Title,
                Summary = e.ShortSummary,
                Section = "Experiences",
            });
        }

        var posts = await journal.GetPublicListingAsync(new JournalPostFilter { Take = 1000 }, ct);

        foreach (var p in posts)
        {
            all.Add(new SitemapEntry(
                $"/journal/{p.Slug}",
                p.UpdatedAt ?? p.PublishedAt ?? p.CreatedAt,
                "monthly",
                0.6,
                CulturesOf(p))
            {
                Title = JournalContent.Title(p, SiteCultures.Default),
                Summary = JournalContent.Resolve(p, SiteCultures.Default)?.Excerpt,
                Section = "Journal",
            });
        }

        var sessions = await seminars.GetPublicListingAsync(new SeminarFilter { Take = 1000 }, ct);

        foreach (var s in sessions)
        {
            all.Add(new SitemapEntry(
                $"/sessions/{s.Slug}",
                s.UpdatedAt ?? s.CreatedAt,
                "monthly",
                0.6,
                CulturesOf(s))
            {
                Title = SeminarContent.Title(s, SiteCultures.Default),
                Section = "Sessions",
            });
        }

        return all;
    }

    /// <summary>
    /// The default language plus whichever others have a translation row.
    ///
    /// This is the difference between an hreflang set that helps and one that hurts: pointing
    /// Google at /tr/journal/some-post when that post has no Turkish copy tells it the page is
    /// translated, it fetches English, and it learns to distrust the annotations on the whole site.
    /// </summary>
    private static IReadOnlyList<string> CulturesOf(Experience e) =>
        Combine(e.Translations.Select(t => t.Culture));

    private static IReadOnlyList<string> CulturesOf(JournalPost p) =>
        Combine(p.Translations.Select(t => t.Culture));

    private static IReadOnlyList<string> CulturesOf(Seminar s) =>
        Combine(s.Translations.Select(t => t.Culture));

    private static IReadOnlyList<string> Combine(IEnumerable<string> translated) =>
    [
        SiteCultures.Default,
        .. translated
            .Where(c => SiteCultures.IsSupported(c) && !string.Equals(c, SiteCultures.Default, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];
}
