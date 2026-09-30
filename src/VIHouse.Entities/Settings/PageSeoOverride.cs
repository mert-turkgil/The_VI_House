using VIHouse.Entities.Common;

namespace VIHouse.Entities.Settings;

/// <summary>
/// What one fixed page (About, Membership, Sessions, …) says in search results and link previews,
/// in one language, as written in Admin > Settings > Pages. Anything left empty falls back to the
/// page's built-in wording. The list of pages lives in SeoPages (Business).
/// </summary>
public class PageSeoOverride : BaseEntity
{
    public Guid SiteSettingId { get; set; }

    /// <summary>SeoPages key: "about", "membership", "sessions", …</summary>
    public string PageKey { get; set; } = default!;
    public string Culture { get; set; } = default!;

    public string? Title { get; set; }
    public string? Description { get; set; }
}
