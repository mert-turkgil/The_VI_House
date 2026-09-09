namespace VIHouse.WebUI.ViewModels.Seo;

/// <summary>
/// What one page wants to say about itself. Controllers set this into ViewData["Seo"]; the layout
/// hands it to SeoResolver, which fills every gap from site settings.
///
/// Every field is optional on purpose. A page that sets nothing still gets a correct title,
/// description, canonical, four hreflang alternates and a social card — because the floor is
/// defined once in the settings screen rather than forgotten once per page.
/// </summary>
public class PageSeo
{
    /// <summary>The page's own name. Run through the language's title template.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// Set when the title is already complete and must not be wrapped — the homepage, whose
    /// template output would otherwise read "Home — The VI House" and waste the most valuable
    /// characters on the site.
    /// </summary>
    public bool TitleIsComplete { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Used only when neither the page nor the settings screen has anything.
    ///
    /// The homepage passes its resource-file copy here rather than as Title/Description, so that
    /// what an admin types into Site &amp; SEO wins — which is the entire point of that screen.
    /// The resource value stays as the seed a fresh install shows before anyone has filled it in.
    /// </summary>
    public string? TitleFallback { get; set; }
    public string? DescriptionFallback { get; set; }

    /// <summary>Absolute or site-relative. Falls back to the language's social image, then the default.</summary>
    public string? ImageUrl { get; set; }
    public string? ImageAlt { get; set; }

    /// <summary>og:type — "website" for most pages, "article" for journal posts, "event" for experiences.</summary>
    public string OgType { get; set; } = "website";

    /// <summary>
    /// Keeps this page out of the index without keeping it out of the site. Account pages, checkout,
    /// search results and anything behind a login: real pages, no business in a search result.
    /// </summary>
    public bool NoIndex { get; set; }

    /// <summary>
    /// The path this page canonically lives at, without the language prefix and without the origin
    /// — "/experiences/zurich-founder-dinner-2026". Left null, the resolver uses the current
    /// request path, which is right for every page that is not paginated or filtered.
    /// </summary>
    public string? CanonicalPath { get; set; }

    /// <summary>
    /// The languages this specific page genuinely exists in. Null means all four.
    ///
    /// This matters more than it looks: hreflang pointing at a URL that renders English is worse
    /// than no hreflang at all, because it tells Google the page is translated when it is not. A
    /// journal post written only in English should say so.
    /// </summary>
    public IReadOnlyList<string>? AvailableCultures { get; set; }

    // --- Article/event extras, emitted only when set ------------------------------------------------

    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public string? AuthorName { get; set; }
    public string? Section { get; set; }

    /// <summary>Extra JSON-LD for this page, already serialised. Emitted as its own script tag.</summary>
    public string? StructuredDataJson { get; set; }

    /// <summary>Trail for the BreadcrumbList, nearest-last. Empty on the homepage.</summary>
    public List<SeoBreadcrumb> Breadcrumbs { get; set; } = [];
}

/// <param name="Path">Site-relative and language-free, as CanonicalPath is.</param>
public record SeoBreadcrumb(string Name, string Path);
