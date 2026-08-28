using VIHouse.Entities.Common;

namespace VIHouse.Entities.Content;

/// <summary>
/// One panel of the homepage hero carousel, managed under Admin → Hero Slides.
///
/// Deliberately a table of its own rather than another ContentBlock: the hero used to be a single
/// block whose copy lived in fixed columns, which has no room for "three of these, in this order,
/// two of them scheduled for next month, each in four languages". Ordering, scheduling and
/// per-culture copy are all first-class here.
///
/// Every word a reader sees lives on <see cref="HeroSlideTranslation"/>. What stays here is the
/// things that are the same in every language — the photograph, the links, when it runs.
/// </summary>
public class HeroSlide : BaseEntity
{
    /// <summary>Ascending. Ties break on CreatedAt so a batch added at once keeps a stable order.</summary>
    public int SortOrder { get; set; }

    /// <summary>Unticked hides the slide without deleting it — the usual way a seasonal panel is
    /// retired, since deleting it would take its four translations with it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// A site path ("/img/hero/lounge-1600.jpg") or an absolute https URL, validated by
    /// SiteImageUrlAttribute on the admin form. Null on a slide whose image was uploaded through
    /// the panel instead — see <see cref="ImageStorageKey"/> — and null on both means the hero
    /// falls back to its plain green ground, which is a legitimate look rather than a broken one.
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Set when the admin uploaded a file rather than pasting a URL: the IMediaStorage key of the
    /// stored image, served publicly by MediaController. Uploads cannot go into wwwroot — it is
    /// mapped by MapStaticAssets, which only serves files that existed at build time, so a file
    /// dropped there at runtime works in Development and 404s in Production.
    /// </summary>
    public string? ImageStorageKey { get; set; }

    /// <summary>
    /// Where the primary button goes. The label is per-language; the destination is not, because a
    /// German reader and an English one both apply on /apply.
    /// </summary>
    public string? PrimaryCtaUrl { get; set; }

    public string? SecondaryCtaUrl { get; set; }

    /// <summary>Optional schedule. Null on both sides means "live as soon as it is active", which
    /// is what the great majority of slides want.</summary>
    public DateTimeOffset? VisibleFromUtc { get; set; }

    public DateTimeOffset? VisibleUntilUtc { get; set; }

    public List<HeroSlideTranslation> Translations { get; set; } = [];
}
