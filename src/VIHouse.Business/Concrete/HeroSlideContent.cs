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
        return TranslationLookup.Best(slide.Translations, culture);
    }

    /// <summary>The exact row for one culture, or null. The admin editor needs this rather than
    /// Resolve, because it must show "not translated yet" instead of quietly displaying English.</summary>
    public static HeroSlideTranslation? Find(HeroSlide slide, string culture) =>
        TranslationLookup.Exact(slide.Translations, culture);

    /// <summary>Convenience for the admin index, which lists slides by their default-culture heading.</summary>
    public static string Heading(HeroSlide slide, string? culture) =>
        Resolve(slide, culture)?.Heading ?? "(untitled slide)";
}
