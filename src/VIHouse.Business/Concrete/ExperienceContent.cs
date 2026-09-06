using VIHouse.Business.Options;
using VIHouse.Entities.Experiences;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Picks the right copy for the reader's language, exactly as SeminarContent, JournalContent and
/// HeroSlideContent do — same chain, same reasoning, so a fifth content type behaves like the other
/// four.
///
/// The one difference is what happens on a complete miss. Those three return null and the caller
/// falls back to a slug; an Experience keeps its English columns on the parent row, so a miss here
/// means "read the parent" and every field has somewhere real to fall back to.
/// </summary>
public static class ExperienceContent
{
    /// <summary>
    /// The best available copy for <paramref name="culture"/>: an exact match, else the same language
    /// in a different region, else the default culture, else nothing — at which point the caller uses
    /// the English columns on the experience itself.
    ///
    /// Unlike the seminar resolver this does *not* fall through to "whatever exists". A German
    /// visitor on an experience translated only into Turkish should read the English original, not
    /// Turkish: the English is always present and always correct, which is not true over there.
    /// </summary>
    public static ExperienceTranslation? Resolve(Experience experience, string? culture)
    {
        if (experience.Translations.Count == 0) return null;

        var wanted = SiteCultures.Normalise(culture);

        return Find(experience, wanted)
            ?? FindByLanguage(experience, wanted);
    }

    /// <summary>
    /// An exact-culture lookup, kept public for the admin editor: it must be able to tell "not
    /// translated yet" from "translated, and falling back to English".
    /// </summary>
    public static ExperienceTranslation? Find(Experience experience, string culture) =>
        experience.Translations.FirstOrDefault(t => string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));

    private static ExperienceTranslation? FindByLanguage(Experience experience, string culture)
    {
        var language = culture.Split('-')[0] + "-";
        return experience.Translations.FirstOrDefault(t => t.Culture.StartsWith(language, StringComparison.OrdinalIgnoreCase));
    }

    // --- Field-by-field readers -------------------------------------------------------------------
    // Each falls back to the experience's own column, so a partially written translation shows the
    // translated fields and the English original for the rest, rather than blanks.

    public static string Title(Experience e, string? culture) =>
        Resolve(e, culture)?.Title is { Length: > 0 } t ? t : e.Title;

    public static string? ShortSummary(Experience e, string? culture) =>
        Resolve(e, culture)?.ShortSummary is { Length: > 0 } s ? s : e.ShortSummary;

    public static string Description(Experience e, string? culture) =>
        Resolve(e, culture)?.Description is { Length: > 0 } d ? d : e.Description;

    public static string? Venue(Experience e, string? culture) =>
        Resolve(e, culture)?.Venue is { Length: > 0 } v ? v : e.Venue;

    public static string? AudienceTags(Experience e, string? culture) =>
        Resolve(e, culture)?.AudienceTags is { Length: > 0 } a ? a : e.AudienceTags;

    public static string? SeoTitle(Experience e, string? culture) =>
        Resolve(e, culture)?.SeoTitle is { Length: > 0 } s ? s : e.SeoTitle;

    public static string? SeoDescription(Experience e, string? culture) =>
        Resolve(e, culture)?.SeoDescription is { Length: > 0 } s ? s : e.SeoDescription;

    public static string? CoverImageAlt(Experience e, string? culture) =>
        Resolve(e, culture)?.CoverImageAlt is { Length: > 0 } a ? a : e.CoverImageAlt;

    /// <summary>
    /// The child rows written in the reader's language, falling back to the default culture's set
    /// when this language has none. Whole-set rather than row-by-row: a half-English, half-Turkish
    /// list of inclusions reads worse than an English one.
    /// </summary>
    public static List<T> ForCulture<T>(IEnumerable<T> rows, string? culture, Func<T, string> cultureOf)
    {
        var all = rows.ToList();
        var wanted = SiteCultures.Normalise(culture);

        var mine = all.Where(r => string.Equals(cultureOf(r), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (mine.Count > 0) return mine;

        var language = wanted.Split('-')[0] + "-";
        var sameLanguage = all.Where(r => cultureOf(r).StartsWith(language, StringComparison.OrdinalIgnoreCase)).ToList();
        if (sameLanguage.Count > 0) return sameLanguage;

        return [.. all.Where(r => string.Equals(cultureOf(r), SiteCultures.Default, StringComparison.OrdinalIgnoreCase))];
    }
}
