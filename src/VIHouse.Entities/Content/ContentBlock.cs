using VIHouse.Entities.Common;

namespace VIHouse.Entities.Content;

/// <summary>
/// One section of a ContentPage. SectionKey maps 1:1 to a homepage ViewComponent (e.g. "hero",
/// "filter-bar", "feature-strip", "ecosystem", "stats", "upcoming", "spotlight", "retreats",
/// "trust"). ExtraJson carries flexible per-section payloads (stat numbers, icon lists,
/// testimonial arrays) that don't warrant their own columns. Testimonial/stat content seeded here
/// must be clearly-labelled placeholders until real figures/quotes exist (brief §15).
/// </summary>
public class ContentBlock : BaseEntity
{
    public Guid PageId { get; set; }
    public string SectionKey { get; set; } = default!;
    public int SortOrder { get; set; }

    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public string? BodyText { get; set; }
    public string? ImageUrl { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public string? ExtraJson { get; set; }

    /// <summary>
    /// This section's copy in the other three languages. The columns above stay as the English
    /// original and the default every other language falls back to, field by field — the same
    /// arrangement Experience uses, and for the same reason: the English values are read from a
    /// dozen places that have no business knowing about cultures.
    /// </summary>
    public List<ContentBlockTranslation> Translations { get; set; } = [];
}
