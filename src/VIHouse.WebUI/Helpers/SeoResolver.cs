using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.Entities.Settings;
using VIHouse.WebUI.ViewModels.Seo;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// Turns a page's own intentions plus the site settings into the finished set of tags.
///
/// One class rather than logic in the layout, because the same answers are needed by three
/// different consumers that must not disagree: the HTML head, the sitemap, and llms.txt. When the
/// canonical host is computed one way in the layout and another way in the sitemap, Google sees two
/// sites.
/// </summary>
public class SeoResolver(
    ISiteSettingsService settingsService,
    IOptions<SiteOptions> siteOptions,
    IHttpContextAccessor httpContextAccessor)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public Task<SiteSetting> SettingsAsync(CancellationToken ct = default) =>
        settingsService.GetCachedAsync(ct);

    /// <summary>
    /// The canonical origin, without a trailing slash.
    ///
    /// Settings first, config second, the live request last. The request is the least trustworthy
    /// of the three: the same page reached through a preview domain, a proxy, an IP address or
    /// "www." would otherwise declare four different canonical homes and split its own ranking.
    /// </summary>
    public string Origin(SiteSetting settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.CanonicalBaseUrl))
            return settings.CanonicalBaseUrl.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(siteOptions.Value.BaseUrl))
            return siteOptions.Value.BaseUrl.TrimEnd('/');

        var request = httpContextAccessor.HttpContext?.Request;
        return request is null ? "" : $"{request.Scheme}://{request.Host}";
    }

    /// <summary>
    /// The path a page lives at in a given language: "/about" in English, "/de/about" in German.
    /// The default culture is unprefixed, which is what makes it the natural x-default.
    /// </summary>
    public static string PathFor(string culture, string path)
    {
        var clean = "/" + path.TrimStart('/');
        var code = SiteCultures.ToUrlCode(culture);
        return code is null ? clean : $"/{code}{(clean == "/" ? "" : clean)}";
    }

    public string UrlFor(SiteSetting settings, string culture, string path) =>
        Origin(settings) + PathFor(culture, path);

    /// <summary>
    /// The settings copy for a culture, falling back to the default's rather than to null — the
    /// same per-field fallback rule the rest of the site's translations follow.
    /// </summary>
    public static SiteSettingTranslation Copy(SiteSetting settings, string culture)
    {
        var normalised = SiteCultures.Normalise(culture);

        return settings.Translations.FirstOrDefault(t => t.Culture == normalised)
            ?? settings.Translations.FirstOrDefault(t => t.Culture == SiteCultures.Default)
            ?? new SiteSettingTranslation { Culture = normalised, SiteName = "The VI House", TitleTemplate = "{0}" };
    }

    /// <summary>Resolves everything the head needs for this request.</summary>
    public ResolvedSeo Resolve(SiteSetting settings, PageSeo page, string culture, string requestPath)
    {
        var copy = Copy(settings, culture);
        var origin = Origin(settings);
        var path = string.IsNullOrWhiteSpace(page.CanonicalPath) ? StripCulture(requestPath) : page.CanonicalPath;

        var title = page.Title switch
        {
            null or "" => Coalesce(copy.HomeTitle, page.TitleFallback, copy.SiteName)!,
            _ when page.TitleIsComplete => page.Title,
            // A page whose own name already contains the site name skips the template. Several
            // experiences are literally called "The VI House — Zurich", and wrapping that produced
            // "The VI House — Zurich — The VI House": the site name twice, in the 60 characters
            // Google actually shows, pushing the one distinguishing word off the end.
            _ when page.Title.Contains(copy.SiteName, StringComparison.OrdinalIgnoreCase) => page.Title,
            _ => SafeFormat(copy.TitleTemplate, page.Title),
        };

        var description = Coalesce(page.Description, copy.DefaultMetaDescription, page.DescriptionFallback);
        var image = Coalesce(page.ImageUrl, copy.OgImageUrl, MediaUrlFor(copy), settings.DefaultOgImageUrl, DefaultImageUrl(settings));

        var cultures = page.AvailableCultures ?? SiteCultures.Names;

        // A page reachable in a language it was never written in — /tr/journal/x with no Turkish
        // copy — serves the English fallback. Pointing its canonical at itself would ask Google to
        // index four URLs of identical English text competing with each other; pointing it at the
        // English URL says "this is that page" and consolidates the lot. The hreflang set below
        // still lists only the languages that genuinely exist, so nothing claims a translation that
        // is not there.
        var writtenInThisLanguage = cultures.Contains(SiteCultures.Normalise(culture), StringComparer.OrdinalIgnoreCase);
        var canonicalCulture = writtenInThisLanguage ? culture : SiteCultures.Default;

        return new ResolvedSeo
        {
            Title = title,
            Description = description,
            CanonicalUrl = origin + PathFor(canonicalCulture, path),
            ImageUrl = Absolute(origin, image),
            ImageAlt = Coalesce(page.ImageAlt, copy.OgImageAlt, description),
            SiteName = copy.SiteName,
            OgType = page.OgType,
            // The settings switch is a master off, not a suggestion: a staging copy with indexing
            // turned off must not be indexable because one page forgot to say so.
            NoIndex = page.NoIndex || !settings.AllowIndexing,
            Culture = SiteCultures.Describe(culture),
            Alternates =
            [
                .. SiteCultures.All
                    .Where(c => cultures.Contains(c.Name, StringComparer.OrdinalIgnoreCase))
                    .Select(c => new SeoAlternate(c.HrefLang, origin + PathFor(c.Name, path), c.OpenGraphLocale)),
            ],
            // x-default points at English, the language the content is authored in and the one a
            // reader with no matching language is best served by.
            DefaultUrl = origin + PathFor(SiteCultures.Default, path),
            TwitterHandle = string.IsNullOrWhiteSpace(settings.TwitterHandle) ? null : "@" + settings.TwitterHandle.TrimStart('@'),
            Page = page,
        };
    }

    // --- JSON-LD --------------------------------------------------------------------------------------

    /// <summary>
    /// Organization + WebSite, emitted once per page. sameAs is what ties the site to its social
    /// profiles in a knowledge panel; without it the accounts and the site are unrelated strangers.
    /// </summary>
    public string OrganizationJsonLd(SiteSetting settings, string culture)
    {
        var copy = Copy(settings, culture);
        var origin = Origin(settings);

        var sameAs = new[]
        {
            settings.InstagramUrl, settings.LinkedInUrl, settings.XUrl,
            settings.FacebookUrl, settings.YouTubeUrl, settings.TikTokUrl,
        }.Where(u => !string.IsNullOrWhiteSpace(u)).ToArray();

        var organization = new Dictionary<string, object?>
        {
            ["@type"] = string.IsNullOrWhiteSpace(settings.OrganizationType) ? "Organization" : settings.OrganizationType,
            ["@id"] = origin + "/#organization",
            ["name"] = copy.SiteName,
            ["legalName"] = settings.LegalName,
            ["url"] = origin + PathFor(culture, "/"),
            ["description"] = Coalesce(copy.OrganizationDescription, copy.DefaultMetaDescription),
            ["foundingDate"] = settings.FoundingDate,
            ["email"] = settings.ContactEmail,
            ["telephone"] = settings.ContactPhone,
            ["sameAs"] = sameAs.Length == 0 ? null : sameAs,
        };

        if (Absolute(origin, Coalesce(settings.LogoUrl, LogoMediaUrl(settings))) is { } logo)
            organization["logo"] = new Dictionary<string, object?> { ["@type"] = "ImageObject", ["url"] = logo };

        if (!string.IsNullOrWhiteSpace(settings.AddressLocality) || !string.IsNullOrWhiteSpace(settings.StreetAddress))
        {
            organization["address"] = new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = settings.StreetAddress,
                ["addressLocality"] = settings.AddressLocality,
                ["addressRegion"] = settings.AddressRegion,
                ["postalCode"] = settings.PostalCode,
                ["addressCountry"] = settings.AddressCountry,
            };
        }

        var website = new Dictionary<string, object?>
        {
            ["@type"] = "WebSite",
            ["@id"] = origin + "/#website",
            ["url"] = origin + PathFor(culture, "/"),
            ["name"] = copy.SiteName,
            ["inLanguage"] = SiteCultures.Normalise(culture),
            ["publisher"] = new Dictionary<string, object?> { ["@id"] = origin + "/#organization" },
            // Lets Google offer a search box scoped to this site directly in the results.
            ["potentialAction"] = new Dictionary<string, object?>
            {
                ["@type"] = "SearchAction",
                ["target"] = new Dictionary<string, object?>
                {
                    ["@type"] = "EntryPoint",
                    ["urlTemplate"] = origin + PathFor(culture, "/search") + "?q={search_term_string}",
                },
                ["query-input"] = "required name=search_term_string",
            },
        };

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new object[] { Prune(organization), website },
        }, Json);
    }

    public string? BreadcrumbJsonLd(SiteSetting settings, PageSeo page, string culture)
    {
        if (page.Breadcrumbs.Count == 0) return null;

        var origin = Origin(settings);

        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = page.Breadcrumbs.Select((b, i) => new Dictionary<string, object?>
            {
                ["@type"] = "ListItem",
                ["position"] = i + 1,
                ["name"] = b.Name,
                ["item"] = origin + PathFor(culture, b.Path),
            }).ToArray(),
        }, Json);
    }

    // --- Helpers --------------------------------------------------------------------------------------

    /// <summary>
    /// Removes a leading language segment, so a canonical path is language-free and the alternates
    /// can be rebuilt for every language from one path.
    /// </summary>
    public static string StripCulture(string path)
    {
        var clean = "/" + path.TrimStart('/');
        var slash = clean.IndexOf('/', 1);
        var first = slash < 0 ? clean[1..] : clean[1..slash];

        if (!SiteCultures.UrlCodes.Contains(first, StringComparer.OrdinalIgnoreCase)) return clean;

        var rest = slash < 0 ? "" : clean[slash..];
        return rest.Length == 0 ? "/" : rest;
    }

    /// <summary>A relative path made absolute against the canonical origin. Absolute URLs pass through.</summary>
    public static string? Absolute(string origin, string? url) =>
        string.IsNullOrWhiteSpace(url) ? null
            : url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
              || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? url
                : origin + "/" + url.TrimStart('/');

    private static string? MediaUrlFor(SiteSettingTranslation copy) =>
        copy.OgImageStorageKey is null ? null : $"/media/site-og/{copy.Id}";

    private static string? LogoMediaUrl(SiteSetting settings) =>
        settings.LogoStorageKey is null ? null : $"/media/site-logo/{settings.Id}";

    private static string? DefaultImageUrl(SiteSetting settings) =>
        settings.DefaultOgImageStorageKey is null ? null : $"/media/site-og-default/{settings.Id}";

    private static string? Coalesce(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>
    /// A title template is admin-editable text, so it can contain a stray brace. string.Format
    /// would throw and take down every page on the site at once; falling back to the bare title is
    /// a far better failure than a 500.
    /// </summary>
    private static string SafeFormat(string template, string value)
    {
        try
        {
            return string.Format(template, value);
        }
        catch (FormatException)
        {
            return value;
        }
    }

    private static Dictionary<string, object?> Prune(Dictionary<string, object?> source) =>
        source.Where(kv => kv.Value is not null and not "").ToDictionary(kv => kv.Key, kv => kv.Value);
}

public class ResolvedSeo
{
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string CanonicalUrl { get; init; } = "";
    public string? ImageUrl { get; init; }
    public string? ImageAlt { get; init; }
    public string SiteName { get; init; } = "";
    public string OgType { get; init; } = "website";
    public bool NoIndex { get; init; }
    public SiteCulture Culture { get; init; } = default!;
    public IReadOnlyList<SeoAlternate> Alternates { get; init; } = [];
    public string DefaultUrl { get; init; } = "";
    public string? TwitterHandle { get; init; }
    public PageSeo Page { get; init; } = new();
}

/// <param name="OgLocale">og:locale form, with an underscore: "de_DE".</param>
public record SeoAlternate(string HrefLang, string Url, string OgLocale);
