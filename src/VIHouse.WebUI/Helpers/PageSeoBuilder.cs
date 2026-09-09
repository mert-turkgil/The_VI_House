using System.Text.Json;
using System.Text.Json.Serialization;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Seminars;
using VIHouse.WebUI.ViewModels.Seo;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// Builds the SEO description of a page from the entity behind it.
///
/// Here rather than in each controller so the rules stay in one place: which cultures a page really
/// exists in, which of the several description-ish fields wins, and what the structured data looks
/// like. Getting any of those subtly different between Experiences and Journal is the kind of thing
/// nobody notices until a rich result stops appearing.
/// </summary>
public static class PageSeoBuilder
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static PageSeo ForExperience(Experience e, string culture, string listingName)
    {
        var title = ExperienceContent.SeoTitle(e, culture) ?? ExperienceContent.Title(e, culture);

        return new PageSeo
        {
            Title = title,
            Description = Trim(
                ExperienceContent.SeoDescription(e, culture)
                ?? ExperienceContent.ShortSummary(e, culture)
                ?? Strip(ExperienceContent.Description(e, culture))),
            ImageUrl = ExperienceService.CoverUrl(e),
            ImageAlt = ExperienceContent.CoverImageAlt(e, culture),
            // schema.org/Event is what produces the date-and-place rich result in search. "website"
            // would render the page as an ordinary document and lose it.
            OgType = "website",
            CanonicalPath = $"/experiences/{e.Slug}",
            AvailableCultures = Cultures(e.Translations.Select(t => t.Culture)),
            ModifiedAt = e.UpdatedAt,
            Breadcrumbs =
            [
                new SeoBreadcrumb(listingName, "/experiences"),
                new SeoBreadcrumb(ExperienceContent.Title(e, culture), $"/experiences/{e.Slug}"),
            ],
            StructuredDataJson = EventJsonLd(e, culture),
        };
    }

    public static PageSeo ForJournalPost(JournalPost post, string culture, string listingName)
    {
        var copy = JournalContent.Resolve(post, culture);

        return new PageSeo
        {
            Title = copy?.SeoTitle ?? JournalContent.Title(post, culture),
            Description = Trim(copy?.SeoDescription ?? copy?.Excerpt ?? Strip(copy?.Body)),
            ImageUrl = post.CoverImageUrl,
            ImageAlt = post.CoverImageAlt,
            OgType = "article",
            CanonicalPath = $"/journal/{post.Slug}",
            AvailableCultures = Cultures(post.Translations.Select(t => t.Culture)),
            PublishedAt = post.PublishedAt,
            ModifiedAt = post.UpdatedAt,
            AuthorName = post.AuthorName,
            Section = post.Category.ToString(),
            Breadcrumbs =
            [
                new SeoBreadcrumb(listingName, "/journal"),
                new SeoBreadcrumb(JournalContent.Title(post, culture), $"/journal/{post.Slug}"),
            ],
        };
    }

    public static PageSeo ForSeminar(Seminar seminar, string culture, string listingName)
    {
        var copy = SeminarContent.Resolve(seminar, culture);

        return new PageSeo
        {
            Title = copy?.SeoTitle ?? SeminarContent.Title(seminar, culture),
            Description = Trim(copy?.SeoDescription ?? Strip(copy?.Summary)),
            CanonicalPath = $"/sessions/{seminar.Slug}",
            AvailableCultures = Cultures(seminar.Translations.Select(t => t.Culture)),
            ModifiedAt = seminar.UpdatedAt,
            Breadcrumbs =
            [
                new SeoBreadcrumb(listingName, "/sessions"),
                new SeoBreadcrumb(SeminarContent.Title(seminar, culture), $"/sessions/{seminar.Slug}"),
            ],
        };
    }

    /// <summary>
    /// schema.org/Event — the thing that turns a listing into a result with a date, a place and a
    /// price attached rather than a blue link.
    ///
    /// eventStatus and eventAttendanceMode are both required by Google for the rich result, and
    /// both are derived from state the admin already sets rather than assumed.
    /// </summary>
    private static string EventJsonLd(Experience e, string culture)
    {
        var cheapest = e.TicketTypes.Count == 0 ? null : e.TicketTypes.MinBy(t => t.PriceMinor);

        var data = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Event",
            ["name"] = ExperienceContent.Title(e, culture),
            ["description"] = Trim(ExperienceContent.ShortSummary(e, culture) ?? Strip(ExperienceContent.Description(e, culture))),
            ["startDate"] = e.StartAtUtc.ToString("o"),
            ["endDate"] = e.EndAtUtc.ToString("o"),
            ["eventStatus"] = e.Status switch
            {
                ExperienceStatus.Completed => "https://schema.org/EventScheduled",
                _ => "https://schema.org/EventScheduled",
            },
            ["eventAttendanceMode"] = e.AttendanceMode switch
            {
                ExperienceAttendanceMode.Online => "https://schema.org/OnlineEventAttendanceMode",
                ExperienceAttendanceMode.Both => "https://schema.org/MixedEventAttendanceMode",
                _ => "https://schema.org/OfflineEventAttendanceMode",
            },
            ["inLanguage"] = SiteCultures.Normalise(culture),
        };

        // A physical event needs a place; an online-only one needs a virtual location instead, and
        // giving it a street address would be a lie about where it happens.
        if (e.AttendanceMode != ExperienceAttendanceMode.Online)
        {
            data["location"] = new Dictionary<string, object?>
            {
                ["@type"] = "Place",
                ["name"] = ExperienceContent.Venue(e, culture) ?? e.City,
                ["address"] = new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["addressLocality"] = e.City,
                    ["addressCountry"] = e.Country,
                },
            };
        }

        if (cheapest is not null)
        {
            data["offers"] = new Dictionary<string, object?>
            {
                ["@type"] = "Offer",
                ["price"] = (cheapest.PriceMinor / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                ["priceCurrency"] = cheapest.Currency,
                ["availability"] = e.Status switch
                {
                    ExperienceStatus.ApplicationsOpen or ExperienceStatus.AlmostFull => "https://schema.org/InStock",
                    ExperienceStatus.Waitlist => "https://schema.org/SoldOut",
                    ExperienceStatus.ComingSoon => "https://schema.org/PreOrder",
                    _ => "https://schema.org/SoldOut",
                },
                ["validFrom"] = e.ApplicationOpenAt?.ToString("o"),
            };
        }

        return JsonSerializer.Serialize(data, Json);
    }

    /// <summary>
    /// The default language plus whichever others actually have a translation row.
    ///
    /// Declaring an hreflang for a language the page is not written in is worse than declaring
    /// none: it tells Google the page is translated, Google fetches it, finds English, and learns
    /// to discount the annotations across the whole site.
    /// </summary>
    private static IReadOnlyList<string> Cultures(IEnumerable<string> translated) =>
    [
        SiteCultures.Default,
        .. translated
            .Where(c => SiteCultures.IsSupported(c)
                && !string.Equals(c, SiteCultures.Default, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>
    /// Descriptions often fall back to a rich-text body. Tags in a meta description render as
    /// literal angle brackets in a search result, so they come out here.
    /// </summary>
    private static string? Strip(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Cut to roughly what a search result shows, on a word boundary. A description truncated
    /// mid-word by Google reads worse than one that ends deliberately.
    /// </summary>
    private static string? Trim(string? value, int limit = 300)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var clean = value.Trim();
        if (clean.Length <= limit) return clean;

        var cut = clean[..limit];
        var lastSpace = cut.LastIndexOf(' ');
        return (lastSpace > 40 ? cut[..lastSpace] : cut).TrimEnd(',', ';', ':', '-') + "…";
    }
}
