using VIHouse.Entities.Common;

namespace VIHouse.Entities.Content;

/// <summary>
/// One culture's worth of a homepage section's copy.
///
/// This table is the reason the homepage below the hero used to read in English no matter which
/// language you switched to. The slider was translated — HeroSlide has had HeroSlideTranslation all
/// along — but everything under it comes from ContentBlocks, and ContentBlocks had exactly one row
/// per section, in English. Nothing was broken; there was simply nowhere to put the other three
/// languages.
///
/// The shape mirrors ContentBlock's own text columns, and follows the same per-field fallback rule
/// the rest of the site uses: an empty field here falls back to the block's English column rather
/// than rendering blank, so a half-finished translation shows what has been written and the
/// original for the rest.
/// </summary>
public class ContentBlockTranslation : BaseEntity
{
    public Guid ContentBlockId { get; set; }

    /// <summary>Culture name exactly as RequestLocalizationOptions supplies it, e.g. "tr-TR".</summary>
    public string Culture { get; set; } = default!;

    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public string? BodyText { get; set; }
    public string? CtaLabel { get; set; }

    /// <summary>
    /// The per-section payload, translated.
    ///
    /// This one carries most of the words on the page and is the reason a Heading-only translation
    /// table would not have been enough: the feature strip's five labels and descriptions, the four
    /// statistic captions, the ecosystem pillars and the testimonials all live inside ExtraJson on
    /// the parent. Translating the heading while leaving "Learn / Connect / Grow" in English would
    /// have looked more broken than not translating the section at all.
    ///
    /// Structure must match the parent's, because the same view models parse both. The admin screen
    /// seeds this field from the English JSON when a language is first opened, so a translator edits
    /// values in a shape that is already correct rather than authoring it from nothing.
    /// </summary>
    public string? ExtraJson { get; set; }
}
