using VIHouse.Entities.Content;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// What a homepage section's editor looks like.
///
/// The shapes are declared here rather than inferred from the JSON, because "what fields does the
/// ecosystem section have" has an answer — Views/Home/_Ecosystem.cshtml renders exactly six per
/// card — and an editor that guesses is how a section ends up with a key the view never reads.
/// </summary>
public record ContentSectionSchema(
    string SectionKey,
    string Title,
    string Description,
    bool UsesHeading,
    bool UsesSubheading,
    bool UsesBody,
    bool UsesCta,
    ContentRowSchema? Rows)
{
    /// <summary>
    /// Every section the homepage actually reads, in the order it renders them. A block whose key is
    /// not on this list still opens — in the raw JSON editor — so an experiment or a section added
    /// by hand is never stranded.
    /// </summary>
    public static readonly IReadOnlyList<ContentSectionSchema> All =
    [
        new("hero", "Hero (fallback)",
            "Only shown when there are no hero slides. Manage the carousel under Hero Slides.",
            UsesHeading: true, UsesSubheading: true, UsesBody: false, UsesCta: true, Rows: null),

        new("feature-strip", "Feature strip",
            "The row of short value statements under the filter bar.",
            UsesHeading: true, UsesSubheading: false, UsesBody: false, UsesCta: false,
            new ContentRowSchema("Feature", ["label", "description"])),

        new("ecosystem", "Ecosystem",
            "The four cards: what the House is beyond the retreats.",
            UsesHeading: true, UsesSubheading: false, UsesBody: true, UsesCta: true,
            new ContentRowSchema("Card", ["title", "description", "imageUrl", "imageAlt", "linkLabel", "linkUrl"])),

        new("stats", "Statistics",
            "The counters. Real numbers only — the brief is explicit about this (§15).",
            UsesHeading: false, UsesSubheading: false, UsesBody: false, UsesCta: false,
            new ContentRowSchema("Statistic", ["value", "label"])),

        new("trust", "Trust",
            "The closing section: what members say.",
            UsesHeading: true, UsesSubheading: true, UsesBody: true, UsesCta: true,
            new ContentRowSchema("Testimonial", ["quote", "author", "role", "avatarUrl"])),

        new("trust-logos", "Trusted by",
            "The scrolling strip of company names beside the testimonials.",
            UsesHeading: true, UsesSubheading: false, UsesBody: false, UsesCta: false,
            new ContentRowSchema("Company", ["name", "imageUrl"])),
    ];

    public static ContentSectionSchema? For(string? sectionKey) =>
        All.FirstOrDefault(s => string.Equals(s.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase));
}

/// <param name="RowLabel">Singular noun for one row, e.g. "Card" — used for headings and buttons.</param>
/// <param name="Fields">JSON property names, in display order. These are the names the homepage's
/// view models bind, so they are also the contract with Views/Home.</param>
public record ContentRowSchema(string RowLabel, string[] Fields);

/// <summary>
/// One row of a section, bound from the form.
///
/// Deliberately one loose bag of fields rather than a type per section. The alternative is five
/// bound models and five near-identical actions, and the fields are only ever written straight back
/// out as JSON — the strong typing that matters lives in ViewModels/Home, where the site reads them.
/// </summary>
public class AdminContentRowViewModel
{
    public Dictionary<string, string?> Values { get; set; } = [];

    public string? this[string field] => Values.GetValueOrDefault(field);
}

/// <summary>One block on the editing screen.</summary>
public class AdminContentSectionViewModel
{
    public Guid Id { get; set; }
    public string SectionKey { get; set; } = default!;
    public int SortOrder { get; set; }
    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public string? BodyText { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? ExtraJson { get; set; }

    /// <summary>Null for a section key the panel has no schema for — that block falls back to the
    /// raw JSON editor.</summary>
    public ContentSectionSchema? Schema { get; set; }

    public List<AdminContentRowViewModel> Rows { get; set; } = [];

    /// <summary>Set when the stored JSON could not be parsed. The editor then shows the raw text and
    /// says so, rather than silently presenting an empty list and offering to save it over the
    /// content that is still live.</summary>
    public string? ParseError { get; set; }
}

/// <summary>The Content screen: one page's sections plus the shared image library.</summary>
public class AdminContentPageViewModel
{
    public Guid PageId { get; set; }
    public string Slug { get; set; } = default!;
    public string Title { get; set; } = default!;
    public List<AdminContentSectionViewModel> Sections { get; set; } = [];
    public List<MediaAsset> Assets { get; set; } = [];
}
