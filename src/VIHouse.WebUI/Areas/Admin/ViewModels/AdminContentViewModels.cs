using VIHouse.Entities.Content;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// What a homepage section's editor looks like.
///
/// The shapes are declared here rather than inferred from the JSON, because "what fields does the
/// ecosystem section have" has an answer — Views/Home/_Ecosystem.cshtml renders exactly six per
/// card — and an editor that guesses is how a section ends up with a key the view never reads.
///
/// The words an editor reads (the section's name, what it is for, the row and field labels) live in
/// SharedResource under Admin.Cms.*, so the panel speaks the editor's interface language.
/// </summary>
public record ContentSectionSchema(
    string SectionKey,
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
        new("hero", UsesHeading: true, UsesSubheading: true, UsesBody: false, UsesCta: true, Rows: null),

        new("feature-strip", UsesHeading: true, UsesSubheading: false, UsesBody: false, UsesCta: false,
            new ContentRowSchema("Feature", ["label", "description"])),

        new("ecosystem", UsesHeading: true, UsesSubheading: true, UsesBody: true, UsesCta: true,
            new ContentRowSchema("Card", ["title", "description", "imageUrl", "imageAlt", "linkLabel", "linkUrl"])),

        new("stats", UsesHeading: false, UsesSubheading: false, UsesBody: false, UsesCta: false,
            new ContentRowSchema("Statistic", ["value", "label"])),

        new("trust", UsesHeading: true, UsesSubheading: true, UsesBody: true, UsesCta: true,
            new ContentRowSchema("Testimonial", ["quote", "author", "role", "avatarUrl"])),

        new("trust-logos", UsesHeading: true, UsesSubheading: false, UsesBody: false, UsesCta: false,
            new ContentRowSchema("Company", ["name", "imageUrl"])),
    ];

    public string TitleKey => $"Admin.Cms.Section.{SectionKey}.Title";

    public string DescriptionKey => $"Admin.Cms.Section.{SectionKey}.Description";

    public static ContentSectionSchema? For(string? sectionKey) =>
        All.FirstOrDefault(s => string.Equals(s.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>The label of one row field (the JSON property name) in the editor's language.</summary>
    public static string FieldKey(string field) => $"Admin.Cms.Field.{field}";
}

/// <param name="Row">What one row is, e.g. "Card". Its heading, blank-row placeholder and removal
/// hint are resource keys built from it.</param>
/// <param name="Fields">JSON property names, in display order. These are the names the homepage's
/// view models bind, so they are also the contract with Views/Home.</param>
public record ContentRowSchema(string Row, string[] Fields)
{
    public string PluralKey => $"Admin.Cms.Row.{Row}.Plural";

    public string NewKey => $"Admin.Cms.Row.{Row}.New";

    public string RemoveHintKey => $"Admin.Cms.Row.{Row}.RemoveHint";
}

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
/// <param name="IsWritten">False when no row exists for this language yet — it falls back to English.</param>
public record AdminContentTranslationTab(
    SiteCulture Culture, bool IsWritten, AdminContentTranslationForm Form)
{
    /// <summary>Which fields this language replaces; everything else follows the English.</summary>
    public List<string> Overrides { get; init; } = [];
}

/// <summary>One language's copy of one homepage section.</summary>
public class AdminContentTranslationForm
{
    public Guid ContentBlockId { get; set; }
    public string Culture { get; set; } = default!;
    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public string? BodyText { get; set; }
    public string? CtaLabel { get; set; }
    public string? ExtraJson { get; set; }
}

public class AdminContentSectionViewModel
{
    /// <summary>
    /// One tab per non-English language. English is absent on purpose: it is not a translation, it
    /// is the block's own columns, edited by the form above these tabs.
    /// </summary>
    public List<AdminContentTranslationTab> Translations { get; set; } = [];

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

    /// <summary>True while the Hero Slides carousel has something to show — the "hero" block is
    /// then a fallback nobody sees.</summary>
    public bool HeroSlidesActive { get; set; }
}
