using VIHouse.Business.Options;
using VIHouse.Entities.Content;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Picks which of a hero slide's translations to show. The hero-shaped twin of
/// <see cref="SeminarContent"/>, and deliberately identical in behaviour — two content types that
/// fall back differently is the kind of inconsistency a reader notices and nobody can explain.
///
/// A pure function over an already-loaded graph, so the homepage can call it per slide without
/// touching the database again.
/// </summary>
public static class HeroSlideContent
{
    /// <summary>
    /// The best available copy for <paramref name="culture"/>: an exact match, else the same
    /// language in another region, else the default culture, else whatever exists. Never null once
    /// the slide has any translation at all — a slide with only Turkish copy shows Turkish rather
    /// than an empty panel.
    /// </summary>
    public static HeroSlideTranslation? Resolve(HeroSlide slide, string? culture)
    {
        if (slide.Translations.Count == 0) return null;

        var wanted = SiteCultures.Normalise(culture);

        return Find(slide, wanted)
            ?? FindByLanguage(slide, wanted)
            ?? Find(slide, SiteCultures.Default)
            ?? slide.Translations[0];
    }

    /// <summary>The exact row for one culture, or null. The admin editor needs this rather than
    /// Resolve, because it must show "not translated yet" instead of quietly displaying English.</summary>
    public static HeroSlideTranslation? Find(HeroSlide slide, string culture) =>
        slide.Translations.FirstOrDefault(t => string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));

    private static HeroSlideTranslation? FindByLanguage(HeroSlide slide, string culture)
    {
        var language = culture.Split('-')[0] + "-";
        return slide.Translations.FirstOrDefault(t => t.Culture.StartsWith(language, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Convenience for the admin index, which lists slides by their default-culture heading.</summary>
    public static string Heading(HeroSlide slide, string? culture) =>
        Resolve(slide, culture)?.Heading ?? "(untitled slide)";
}
