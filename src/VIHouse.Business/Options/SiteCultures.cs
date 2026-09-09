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
        new("en-GB", "EN", "English", "en"),
        new("de-DE", "DE", "Deutsch", "de"),
        new("tr-TR", "TR", "Türkçe", "tr"),
        new("et-EE", "ET", "Eesti", "et"),
    ];

    /// <summary>
    /// The short codes that appear in URLs, minus the default's.
    ///
    /// English is served unprefixed (/experiences) and the rest are prefixed (/de/experiences).
    /// That keeps every existing link, bookmark and inbound backlink working exactly as it did,
    /// and it makes the English URL the natural x-default in the hreflang set.
    /// </summary>
    public static readonly string[] UrlCodes = [.. All.Where(c => c.Name != Default).Select(c => c.UrlCode)];

    /// <summary>Every short code including the default's — for building hreflang sets.</summary>
    public static readonly string[] AllUrlCodes = [.. All.Select(c => c.UrlCode)];

    /// <summary>The full culture behind a URL segment ("de" -> "de-DE"), or null if it is not one.</summary>
    public static string? FromUrlCode(string? code) =>
        code is null ? null
            : All.FirstOrDefault(c => string.Equals(c.UrlCode, code, StringComparison.OrdinalIgnoreCase))?.Name;

    /// <summary>The URL segment for a culture, or null for the default, which has none.</summary>
    public static string? ToUrlCode(string? culture)
    {
        var name = Normalise(culture);
        return name == Default ? null : Describe(name).UrlCode;
    }

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
/// <param name="UrlCode">Lower-case segment used in the URL, e.g. "de" in /de/experiences.</param>
public record SiteCulture(string Name, string ShortLabel, string NativeLabel, string UrlCode)
{
    /// <summary>The BCP-47 tag for hreflang and og:locale. Same as Name, but named for its use.</summary>
    public string HrefLang => Name;

    /// <summary>og:locale wants underscores: "en_GB", not "en-GB".</summary>
    public string OpenGraphLocale => Name.Replace('-', '_');
}
