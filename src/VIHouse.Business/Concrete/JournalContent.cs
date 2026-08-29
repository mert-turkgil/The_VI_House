using VIHouse.Business.Options;
using VIHouse.Entities.Journal;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Picks which of a post's translations to show — the journal-shaped twin of
/// <see cref="SeminarContent"/> and <see cref="HeroSlideContent"/>, and deliberately identical in
/// behaviour. Three content types that fall back differently is the kind of inconsistency a reader
/// notices and nobody can explain.
///
/// A pure function over an already-loaded graph, so a listing can call it once per card without
/// touching the database again.
/// </summary>
public static class JournalContent
{
    /// <summary>
    /// The best available copy for <paramref name="culture"/>: an exact match, else the same
    /// language in another region, else the default culture, else whatever exists. Never null once
    /// the post has any translation at all — a post with only Turkish copy shows Turkish rather
    /// than an empty page.
    /// </summary>
    public static JournalPostTranslation? Resolve(JournalPost post, string? culture)
    {
        if (post.Translations.Count == 0) return null;

        var wanted = SiteCultures.Normalise(culture);

        return Find(post, wanted)
            ?? FindByLanguage(post, wanted)
            ?? Find(post, SiteCultures.Default)
            ?? post.Translations[0];
    }

    /// <summary>The exact row for one culture, or null. The admin editor needs this rather than
    /// Resolve, because it must show "not translated yet" instead of quietly displaying English.</summary>
    public static JournalPostTranslation? Find(JournalPost post, string culture) =>
        post.Translations.FirstOrDefault(t => string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));

    private static JournalPostTranslation? FindByLanguage(JournalPost post, string culture)
    {
        var language = culture.Split('-')[0] + "-";
        return post.Translations.FirstOrDefault(t => t.Culture.StartsWith(language, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Convenience for the admin index and the audit log, which list posts by headline.</summary>
    public static string Title(JournalPost post, string? culture) =>
        Resolve(post, culture)?.Title ?? post.Slug;
}
