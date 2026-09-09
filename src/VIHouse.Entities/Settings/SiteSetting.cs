using VIHouse.Entities.Common;

namespace VIHouse.Entities.Settings;

/// <summary>
/// One row. Everything the site needs to describe itself to a search engine, a social network, a
/// messaging app's link preview, or a language model.
///
/// Singleton by convention rather than by constraint: the service reads the first row and creates
/// one if the table is empty, which is the same shape ContentPage's "home" row already uses. A
/// settings table with two rows would be a bug, but a check constraint that makes seeding harder is
/// not worth preventing something no code path can do.
///
/// The split between here and SiteSettingTranslation is the important part: anything a reader sees
/// as words lives in the translation, anything that is the same fact in every language lives here.
/// A Turkish visitor should get a Turkish description; the company's founding date is a date.
/// </summary>
public class SiteSetting : BaseEntity
{
    // --- Identity ---------------------------------------------------------------------------------

    /// <summary>
    /// Canonical origin, e.g. "https://thevihouse.com" — no trailing slash. Every absolute URL the
    /// SEO layer emits is built from this rather than from the incoming request, so a page fetched
    /// through a preview host, a proxy, or "www." still declares one canonical home. Falls back to
    /// Site:BaseUrl when empty.
    /// </summary>
    public string? CanonicalBaseUrl { get; set; }

    /// <summary>schema.org type for the organisation JSON-LD. "Organization" suits most; a physical
    /// venue may want "LocalBusiness".</summary>
    public string OrganizationType { get; set; } = "Organization";

    public string? LegalName { get; set; }
    public string? FoundingDate { get; set; }

    /// <summary>Square logo, absolute or site-relative. Google wants at least 112x112 for this.</summary>
    public string? LogoUrl { get; set; }
    public string? LogoStorageKey { get; set; }

    // --- Default social image ---------------------------------------------------------------------

    /// <summary>
    /// The picture that appears when a link is pasted anywhere — WhatsApp, Slack, LinkedIn, X.
    /// A page with its own image wins; this is the floor, so no link ever previews as a bare
    /// grey box. 1200x630 is the size every network agrees on.
    /// </summary>
    public string? DefaultOgImageUrl { get; set; }
    public string? DefaultOgImageStorageKey { get; set; }

    // --- Social accounts --------------------------------------------------------------------------
    // Emitted as schema.org sameAs, which is how a search engine ties the site to its profiles.

    public string? InstagramUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? XUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? TikTokUrl { get; set; }

    /// <summary>The @handle, with or without the @ — used for twitter:site on the card.</summary>
    public string? TwitterHandle { get; set; }

    // --- Contact / place --------------------------------------------------------------------------

    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? StreetAddress { get; set; }
    public string? AddressLocality { get; set; }
    public string? AddressRegion { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>ISO 3166-1 alpha-2, e.g. "GB".</summary>
    public string? AddressCountry { get; set; }

    // --- Search console verification --------------------------------------------------------------
    // The bare token, not the whole meta tag. Pasting the full tag is the common mistake, so the
    // admin field says so and the renderer strips one if it arrives anyway.

    public string? GoogleSiteVerification { get; set; }
    public string? BingSiteVerification { get; set; }

    // --- Crawling ---------------------------------------------------------------------------------

    /// <summary>
    /// The master switch. False emits "Disallow: /" and a noindex on every page — which is what a
    /// staging copy needs, and the single most valuable setting here, because a staging site
    /// indexed alongside production outranks nothing and confuses everything.
    /// </summary>
    public bool AllowIndexing { get; set; } = true;

    /// <summary>Appended verbatim to robots.txt, for a rule this screen has no field for.</summary>
    public string? RobotsExtra { get; set; }

    /// <summary>
    /// Whether to publish /llms.txt — the plain-language index of the site for language models
    /// (llmstxt.org). Separate from AllowIndexing, because "search engines yes, model training
    /// maybe" is a real and reasonable position to hold.
    /// </summary>
    public bool PublishLlmsTxt { get; set; } = true;

    public List<SiteSettingTranslation> Translations { get; set; } = [];
}
