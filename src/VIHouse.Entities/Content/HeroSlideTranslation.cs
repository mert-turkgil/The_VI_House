using VIHouse.Entities.Common;

namespace VIHouse.Entities.Content;

/// <summary>
/// One culture's worth of a hero slide's copy. Mirrors SeminarTranslation: a row exists per
/// language the admin has actually written, and a missing row falls back to the default culture at
/// read time (see HeroSlideContent) rather than rendering an empty panel.
///
/// All plain text — no editor HTML anywhere in the hero, so nothing here needs sanitising and
/// everything can be rendered with ordinary Razor encoding.
/// </summary>
public class HeroSlideTranslation : BaseEntity
{
    public Guid HeroSlideId { get; set; }

    /// <summary>Culture name exactly as RequestLocalizationOptions supplies it, e.g. "en-GB".</summary>
    public string Culture { get; set; } = default!;

    /// <summary>Small caps line above the heading. Optional — most slides do without one.</summary>
    public string? Eyebrow { get; set; }

    public string Heading { get; set; } = default!;

    public string? Subheading { get; set; }

    public string? PrimaryCtaLabel { get; set; }

    public string? SecondaryCtaLabel { get; set; }

    /// <summary>
    /// Alt text for the photograph, per language because it is prose a screen reader speaks. Empty
    /// is correct for a slide whose image is purely atmospheric behind the heading — see the
    /// guidance in _CoverImage.cshtml.
    /// </summary>
    public string? ImageAlt { get; set; }
}
