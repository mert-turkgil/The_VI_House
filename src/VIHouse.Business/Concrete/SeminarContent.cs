using VIHouse.Business.Options;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Picks which of a seminar's translations to show.
///
/// Separate from SeminarService because the read side needs it everywhere — listings, detail pages,
/// the admin index, the enrolment email — and none of those should have to hold a service reference
/// just to work out which title to print. It is a pure function over an already-loaded graph, so it
/// costs nothing to call twice.
/// </summary>
public static class SeminarContent
{
    /// <summary>
    /// The best available copy for <paramref name="culture"/>: an exact match, else the same
    /// language in a different region, else the default culture, else whatever exists.
    ///
    /// Falling all the way through to "whatever exists" rather than returning null is deliberate. A
    /// seminar with a Turkish body and no English one is a half-finished translation, and showing
    /// the Turkish is closer to what the admin meant than showing an empty page — publishing is
    /// already gated on the default culture existing (see SeminarService.SetStatusAsync), so this
    /// last fallback only ever fires for content that is still in draft.
    /// </summary>
    public static SeminarTranslation? Resolve(Seminar seminar, string? culture)
    {
        if (seminar.Translations.Count == 0) return null;

        var wanted = SiteCultures.Normalise(culture);

        return Find(seminar, wanted)
            ?? FindByLanguage(seminar, wanted)
            ?? Find(seminar, SiteCultures.Default)
            ?? seminar.Translations[0];
    }

    /// <summary>The exact row for one culture, or null. Used by the admin editor, which must be
    /// able to tell "not translated yet" from "translated, falls back to English".</summary>
    public static SeminarTranslation? Find(Seminar seminar, string culture) =>
        seminar.Translations.FirstOrDefault(t => string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));

    private static SeminarTranslation? FindByLanguage(Seminar seminar, string culture)
    {
        var language = culture.Split('-')[0] + "-";
        return seminar.Translations.FirstOrDefault(t => t.Culture.StartsWith(language, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Convenience for the many places that only need a heading.</summary>
    public static string Title(Seminar seminar, string? culture) =>
        Resolve(seminar, culture)?.Title ?? seminar.Slug;
}
