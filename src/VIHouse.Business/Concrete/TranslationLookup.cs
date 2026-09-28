using VIHouse.Business.Options;
using VIHouse.Entities.Common;

namespace VIHouse.Business.Concrete;

/// <summary>
/// The one way the site picks a translation row for a reader's language. Every content type
/// (ExperienceContent, SeminarContent, JournalContent, HeroSlideContent, ContentBlockContent) goes
/// through here, so they all follow the same chain:
///
///   exact culture → same language, other region → (optionally) default culture → any row.
///
/// Types whose parent row keeps its own English columns (experiences, content blocks) stop after
/// the second step and read the parent instead — see <see cref="Closest{T}"/>. The rest use
/// <see cref="Best{T}"/>, which never returns null once any translation exists.
/// </summary>
public static class TranslationLookup
{
    /// <summary>The row for exactly this culture, or null — what the admin editors need to tell
    /// "not translated yet" from "falling back to English".</summary>
    public static T? Exact<T>(IEnumerable<T> rows, string culture) where T : class, ITranslation =>
        rows.FirstOrDefault(t => string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));

    /// <summary>Exact culture, else the same language in another region ("de-AT" for "de-DE").</summary>
    public static T? Closest<T>(IReadOnlyCollection<T> rows, string? culture) where T : class, ITranslation
    {
        if (rows.Count == 0) return null;

        var wanted = SiteCultures.Normalise(culture);
        var language = wanted.Split('-')[0] + "-";
        return Exact(rows, wanted)
            ?? rows.FirstOrDefault(t => t.Culture.StartsWith(language, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary><see cref="Closest{T}"/>, else the default culture, else whatever exists.</summary>
    public static T? Best<T>(IReadOnlyCollection<T> rows, string? culture) where T : class, ITranslation =>
        Closest(rows, culture) ?? Exact(rows, SiteCultures.Default) ?? rows.FirstOrDefault();
}
