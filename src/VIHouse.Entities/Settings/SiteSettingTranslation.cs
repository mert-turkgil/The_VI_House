using VIHouse.Entities.Common;

namespace VIHouse.Entities.Settings;

/// <summary>
/// The site's own words, per language. One row per culture the site speaks.
///
/// This is what makes a shared link correct: a Turkish URL previews with a Turkish title and a
/// Turkish description, because a link preview reads the meta tags of whatever URL it was given —
/// and until the URL carried a language, there was only ever one set of tags to read.
/// </summary>
public class SiteSettingTranslation : BaseEntity
{
    public Guid SiteSettingId { get; set; }

    /// <summary>e.g. "tr-TR".</summary>
    public string Culture { get; set; } = default!;

    /// <summary>What the house calls itself in this language. Usually the same, occasionally not.</summary>
    public string SiteName { get; set; } = default!;

    /// <summary>
    /// How a page title is assembled, with {0} for the page's own title —
    /// "{0} — The VI House". A page with no title of its own uses HomeTitle instead.
    /// German and Turkish take a different separator convention, which is precisely why this is
    /// per-language and not a constant in the layout.
    /// </summary>
    public string TitleTemplate { get; set; } = "{0} — The VI House";

    /// <summary>The homepage's own title. Not run through the template — a home page titled
    /// "Home — The VI House" wastes the most valuable 60 characters on the site.</summary>
    public string? HomeTitle { get; set; }

    /// <summary>
    /// The description used by any page that does not set its own, and by the homepage. This is
    /// the sentence under the blue link in Google, so it is written for a person, not a crawler.
    /// </summary>
    public string? DefaultMetaDescription { get; set; }

    /// <summary>One-line description of the whole site, for schema.org and llms.txt.</summary>
    public string? OrganizationDescription { get; set; }

    /// <summary>Alt text for the default social image, in this language.</summary>
    public string? OgImageAlt { get; set; }

    /// <summary>
    /// Per-language social image. Optional and usually empty — but when the image has words baked
    /// into it, an English card under a Turkish headline looks like a mistake, and this is the
    /// only way to fix it.
    /// </summary>
    public string? OgImageUrl { get; set; }
    public string? OgImageStorageKey { get; set; }
}
