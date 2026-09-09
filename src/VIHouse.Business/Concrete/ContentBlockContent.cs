using VIHouse.Business.Options;
using VIHouse.Entities.Content;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Picks the right homepage copy for the reader's language — the same chain, and the same
/// reasoning, as ExperienceContent, SeminarContent, JournalContent and HeroSlideContent, so a sixth
/// content type behaves like the other five.
///
/// Like the experience resolver and unlike the seminar one, a complete miss means "read the parent"
/// rather than "read whatever exists": the English columns live on ContentBlock itself and are
/// always present, so a German visitor on a section translated only into Turkish gets the English
/// original rather than Turkish.
/// </summary>
public static class ContentBlockContent
{
    /// <summary>
    /// The best available copy for <paramref name="culture"/>: an exact match, else the same
    /// language in a different region, else nothing — at which point the caller uses the block's own
    /// columns.
    /// </summary>
    public static ContentBlockTranslation? Resolve(ContentBlock? block, string? culture)
    {
        if (block is null || block.Translations.Count == 0) return null;

        var wanted = SiteCultures.Normalise(culture);
        return Find(block, wanted) ?? FindByLanguage(block, wanted);
    }

    /// <summary>
    /// An exact-culture lookup, kept public for the admin editor: it has to be able to tell "not
    /// translated yet" from "translated, and falling back to English".
    /// </summary>
    public static ContentBlockTranslation? Find(ContentBlock block, string culture) =>
        block.Translations.FirstOrDefault(t => string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));

    private static ContentBlockTranslation? FindByLanguage(ContentBlock block, string culture)
    {
        var language = culture.Split('-')[0] + "-";
        return block.Translations.FirstOrDefault(t => t.Culture.StartsWith(language, StringComparison.OrdinalIgnoreCase));
    }

    // --- Field-by-field readers -------------------------------------------------------------------
    // Each falls back to the block's own column, so a half-written translation shows the translated
    // fields and the English original for the rest rather than blanks. Every one of these accepts a
    // null block, because the homepage looks its sections up by key and a section that has not been
    // created yet is an ordinary state, not an error.

    public static string? Heading(ContentBlock? b, string? culture) =>
        Pick(Resolve(b, culture)?.Heading, b?.Heading);

    public static string? Subheading(ContentBlock? b, string? culture) =>
        Pick(Resolve(b, culture)?.Subheading, b?.Subheading);

    public static string? BodyText(ContentBlock? b, string? culture) =>
        Pick(Resolve(b, culture)?.BodyText, b?.BodyText);

    public static string? CtaLabel(ContentBlock? b, string? culture) =>
        Pick(Resolve(b, culture)?.CtaLabel, b?.CtaLabel);

    /// <summary>
    /// The translated payload, or the original when this language has not written one.
    ///
    /// Whole-field rather than merged item by item: the arrays in here are ordered lists whose shape
    /// the section's view model parses directly, and merging a three-item Turkish list into a
    /// five-item English one by index would produce a section that is partly translated and silently
    /// wrong about which description belongs to which label. All of it or none of it is the only
    /// version a translator can reason about.
    /// </summary>
    public static string? ExtraJson(ContentBlock? b, string? culture) =>
        Pick(Resolve(b, culture)?.ExtraJson, b?.ExtraJson);

    private static string? Pick(string? translated, string? original) =>
        string.IsNullOrWhiteSpace(translated) ? original : translated;
}
