using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// The three files nothing links to and everything reads: sitemap.xml, robots.txt and llms.txt.
///
/// Generated rather than static, because all three have to agree with the site as it actually is —
/// a static sitemap committed to the repository is accurate on the day it is written and wrong from
/// the next publish onwards.
///
/// Excluded from the language-prefix convention (see CulturePrefixConvention.IsExcluded): these are
/// single documents that describe every language at once, and /de/sitemap.xml would be a second
/// copy of the same thing.
/// </summary>
public class SeoController(
    ISitemapService sitemap,
    ISiteSettingsService settingsService,
    SeoResolver seo,
    IOptionsMonitor<FeatureOptions> features) : Controller
{
    private static readonly XNamespace Sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private static readonly XNamespace Xhtml = "http://www.w3.org/1999/xhtml";

    /// <summary>
    /// Every public URL, in every language it exists in, with the alternates spelled out.
    ///
    /// The xhtml:link annotations are the part that matters here and the part most sitemaps omit:
    /// they tell Google that /experiences/x and /de/experiences/x are the same page in two
    /// languages rather than two competing pages, which is what stops them cannibalising each
    /// other's ranking.
    /// </summary>
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct)
    {
        var settings = await settingsService.GetCachedAsync(ct);

        // A site that has told us not to index it should not be handing out a map of itself.
        if (!settings.AllowIndexing) return NotFound();

        var origin = seo.Origin(settings);

        // Behind the launch curtain the site has exactly one public page, so the map says so.
        // Without this the sitemap keeps advertising every URL on the site while ComingSoonGate
        // redirects all of them to /coming-soon — handing a crawler a list of several dozen
        // addresses that all resolve to the same page, which is the textbook shape of a site that
        // gets its rankings discounted for duplicate content.
        //
        // The entry replaces the list rather than filtering it, so the loop below still writes the
        // full hreflang alternate set and the curtain is correctly declared in all four languages.
        var entries = features.CurrentValue.ComingSoon
            ? [new SitemapEntry("/coming-soon", null, "daily", 1.0, SiteCultures.All.Select(c => c.Name).ToList())]
            : await sitemap.GetEntriesAsync(ct);

        var urlset = new XElement(Sm + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", Xhtml));

        foreach (var entry in entries)
        {
            var cultures = entry.Cultures.Where(SiteCultures.IsSupported).ToList();
            if (cultures.Count == 0) continue;

            // One <url> per language, each carrying the full alternate set including itself, which
            // is what the specification requires — an entry that omits itself is discarded whole.
            foreach (var culture in cultures)
            {
                var url = new XElement(Sm + "url",
                    new XElement(Sm + "loc", origin + SeoResolver.PathFor(culture, entry.Path)));

                if (entry.LastModified is { } modified)
                {
                    url.Add(new XElement(Sm + "lastmod",
                        modified.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                }

                url.Add(new XElement(Sm + "changefreq", entry.ChangeFrequency));
                url.Add(new XElement(Sm + "priority", entry.Priority.ToString("0.0", CultureInfo.InvariantCulture)));

                foreach (var alternate in cultures)
                {
                    url.Add(new XElement(Xhtml + "link",
                        new XAttribute("rel", "alternate"),
                        new XAttribute("hreflang", alternate),
                        new XAttribute("href", origin + SeoResolver.PathFor(alternate, entry.Path))));
                }

                url.Add(new XElement(Xhtml + "link",
                    new XAttribute("rel", "alternate"),
                    new XAttribute("hreflang", "x-default"),
                    new XAttribute("href", origin + SeoResolver.PathFor(SiteCultures.Default, entry.Path))));

                urlset.Add(url);
            }
        }

        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        return Fresh(document.Declaration + Environment.NewLine + document, "application/xml");
    }

    /// <summary>
    /// Generated so that the master indexing switch on the settings screen is real.
    ///
    /// The disallow list is the funnel and the private area: pages that exist, work, and have no
    /// business in a search result. /search is excluded because a search box generates unbounded
    /// near-duplicate URLs, which is the standard way to spend a crawl budget on nothing.
    /// </summary>
    [HttpGet("/robots.txt")]
    public async Task<IActionResult> Robots(CancellationToken ct)
    {
        var settings = await settingsService.GetCachedAsync(ct);
        var origin = seo.Origin(settings);
        var builder = new StringBuilder();

        builder.AppendLine("User-agent: *");

        if (!settings.AllowIndexing)
        {
            // Staging. Worth saying out loud in the file itself, because the first thing anyone
            // does when a site will not index is open robots.txt and wonder who did this.
            builder.AppendLine("Disallow: /");
            builder.AppendLine();
            builder.AppendLine("# Indexing is switched off in Admin > Site settings > Search visibility.");
            return Fresh(builder.ToString(), "text/plain");
        }

        foreach (var path in new[]
        {
            "/admin/", "/account/", "/checkout/", "/onboarding/",
            "/login", "/logout", "/forgot-password", "/reset-password", "/confirm-email", "/access-denied", "/register",
            "/invitation/", "/r/", "/webhooks/", "/culture/", "/search",
        })
        {
            builder.AppendLine($"Disallow: {path}");
        }

        // Media is allowed on purpose: blocking it would keep every og:image and every cover
        // photograph out of image search and out of the link previews that fetch them.
        builder.AppendLine("Allow: /media/");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(settings.RobotsExtra))
        {
            builder.AppendLine(settings.RobotsExtra.Trim());
            builder.AppendLine();
        }

        builder.AppendLine($"Sitemap: {origin}/sitemap.xml");

        if (settings.PublishLlmsTxt)
        {
            builder.AppendLine($"# Plain-language index for language models: {origin}/llms.txt");
        }

        return Fresh(builder.ToString(), "text/plain");
    }

    /// <summary>
    /// llms.txt — the site, in Markdown, for a language model that has been asked about it
    /// (llmstxt.org).
    ///
    /// The format is deliberately plain: an H1 with the site name, a blockquote summary, then
    /// sections of links with one line of description each. A model that fetches this gets an
    /// accurate map of what the site is and where things are, instead of inferring it from
    /// whichever page it happened to land on.
    ///
    /// Its own switch, separate from AllowIndexing: "search engines yes, models maybe" is a
    /// coherent position and the two should not be one checkbox.
    /// </summary>
    [HttpGet("/llms.txt")]
    public async Task<IActionResult> LlmsTxt(string? lang, CancellationToken ct)
    {
        var settings = await settingsService.GetCachedAsync(ct);
        if (!settings.PublishLlmsTxt) return NotFound();

        // ?lang=de serves the German notes with German links; no parameter is the English file.
        var culture = SiteCultures.FromUrlCode(lang) ?? SiteCultures.Default;
        var origin = seo.Origin(settings);
        var copy = SeoResolver.Copy(settings, culture);
        var english = SeoResolver.Copy(settings, SiteCultures.Default);

        // Behind the launch curtain every page redirects to /coming-soon, so that is all this lists —
        // the same narrowing the sitemap applies.
        var entries = features.CurrentValue.ComingSoon
            ? [new SitemapEntry("/coming-soon", null, "daily", 1.0, SiteCultures.Names) { Title = copy.SiteName }]
            : await sitemap.GetEntriesAsync(ct);

        var builder = new StringBuilder();
        builder.AppendLine($"# {copy.SiteName}");
        builder.AppendLine();

        var summary = copy.OrganizationDescription ?? copy.DefaultMetaDescription
            ?? english.OrganizationDescription ?? english.DefaultMetaDescription;
        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.AppendLine($"> {Flatten(summary)}");
            builder.AppendLine();
        }

        // The owner's own words for assistants (Admin > Settings), in this language or else English.
        var notes = copy.LlmsNotes ?? english.LlmsNotes;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            builder.AppendLine(notes.Trim().Replace("\r\n", "\n"));
            builder.AppendLine();
        }
        else
        {
            builder.AppendLine(
                $"This site is published in {SiteCultures.All.Count} languages: " +
                string.Join(", ", SiteCultures.All.Select(c => $"{c.NativeLabel} ({c.HrefLang})")) + ". " +
                $"English is served at the unprefixed path and the others under a language prefix, " +
                $"so {origin}/experiences and {origin}/de/experiences are the same page in two languages.");
            builder.AppendLine();
        }

        var otherFiles = SiteCultures.All.Where(c => c.Name != culture)
            .Select(c => c.Name == SiteCultures.Default ? $"{origin}/llms.txt" : $"{origin}/llms.txt?lang={c.UrlCode}");
        builder.AppendLine("Other languages: " + string.Join(", ", otherFiles));
        builder.AppendLine();

        foreach (var section in entries.GroupBy(e => e.Section).OrderBy(g => SectionOrder(g.Key)))
        {
            builder.AppendLine($"## {section.Key}");
            builder.AppendLine();

            foreach (var entry in section.OrderByDescending(e => e.Priority).ThenBy(e => e.Path))
            {
                var label = string.IsNullOrWhiteSpace(entry.Title) ? PrettyName(entry.Path) : entry.Title;
                var line = $"- [{Flatten(label)}]({origin}{SeoResolver.PathFor(culture, entry.Path)})";

                if (!string.IsNullOrWhiteSpace(entry.Summary))
                    line += $": {Flatten(entry.Summary)}";

                builder.AppendLine(line);
            }

            builder.AppendLine();
        }

        return Fresh(builder.ToString(), "text/plain");
    }

    /// <summary>
    /// Always revalidated, never stale: an admin edit shows on the very next fetch. A crawler that
    /// sends back the ETag of what it already has gets an empty 304 instead of the whole file, so
    /// "never cached" costs almost nothing.
    /// </summary>
    private IActionResult Fresh(string body, string mediaType)
    {
        var etag = $"\"{Text.Sha256Hex(body)[..32]}\"";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.ETag = etag;

        if (Request.Headers.IfNoneMatch.Any(v => v is not null && v.Split(',').Any(t => t.Trim() == etag || t.Trim() == "*")))
            return StatusCode(StatusCodes.Status304NotModified);

        return Content(body, mediaType, Encoding.UTF8);
    }

    private static int SectionOrder(string section) => section switch
    {
        "Pages" => 0,
        "Experiences" => 1,
        "Journal" => 2,
        "Sessions" => 3,
        _ => 9,
    };

    /// <summary>"/legal/terms" becomes "Legal terms" — a readable label for a page with no title.</summary>
    private static string PrettyName(string path)
    {
        if (path == "/") return "Home";

        var words = path.Trim('/').Replace('/', ' ').Replace('-', ' ');
        return words.Length == 0 ? "Home" : char.ToUpperInvariant(words[0]) + words[1..];
    }

    /// <summary>
    /// Markdown links break across a newline, and descriptions come from a rich-text editor that
    /// happily produces them. Collapsed to single spaces so one stray paragraph cannot corrupt the
    /// whole document's structure.
    /// </summary>
    private static string Flatten(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
