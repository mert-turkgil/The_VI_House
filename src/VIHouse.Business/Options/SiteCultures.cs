namespace VIHouse.Business.Options;

/// <summary>
/// The languages the site speaks (brief: EN/DE/TR/ET), in one place.
///
/// This list used to be written out three times — the supported-culture array in Program.cs, the
/// accept-list in CultureController, and the label table in _Nav.cshtml — which is exactly the kind
/// of duplication that ends with a language that switches but does not translate. Seminar content
/// is stored per culture (SeminarTranslation), so a fourth copy was about to appear; consolidating
/// first was cheaper than keeping them in step.
///
/// Lives in Business rather than WebUI because the seminar layer resolves translations against it
/// and cannot reference the web project.
/// </summary>
public static class SiteCultures
{
    /// <summary>The fallback for everything: the culture content is authored in first, and what a
    /// reader gets when their own language has no translation yet.</summary>
    public const string Default = "en-GB";

    public static readonly IReadOnlyList<SiteCulture> All =
    [
        new("en-GB", "EN", "English"),
        new("de-DE", "DE", "Deutsch"),
        new("tr-TR", "TR", "Türkçe"),
        new("et-EE", "ET", "Eesti"),
    ];

    /// <summary>Culture names in declaration order — Default first, which is what
    /// RequestLocalizationOptions.SetDefaultCulture relies on.</summary>
    public static readonly string[] Names = [.. All.Select(c => c.Name)];

    public static bool IsSupported(string? culture) =>
        culture is not null && Names.Contains(culture, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps whatever the request carried onto a culture we actually have content for. Falls back
    /// through the language part first, so a browser asking for "de-AT" or a cookie left over from
    /// "tr" still lands on German or Turkish rather than dropping all the way to English.
    /// </summary>
    public static string Normalise(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture)) return Default;

        var exact = Names.FirstOrDefault(n => string.Equals(n, culture, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var language = culture.Split('-')[0];
        var byLanguage = Names.FirstOrDefault(n => n.StartsWith(language + "-", StringComparison.OrdinalIgnoreCase));

        return byLanguage ?? Default;
    }

    public static SiteCulture Describe(string? culture)
    {
        var name = Normalise(culture);
        return All.First(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}

/// <param name="Name">Culture name, e.g. "de-DE".</param>
/// <param name="ShortLabel">Two-letter label for the compact language switcher.</param>
/// <param name="NativeLabel">The language's name in itself — never translated, by design.</param>
public record SiteCulture(string Name, string ShortLabel, string NativeLabel);
