using System.Globalization;
using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// Resolves a key against the .resx files on disk, walking the culture chain by hand.
///
/// The chain is short because the resx suffixes are two-letter while the site's cultures are four:
/// de-DE reads SharedResource.de.resx and then falls back to SharedResource.resx. English is the
/// neutral file itself, so en-GB has a chain of one. ResourceManager does this walk for you; doing
/// it ourselves is the price of reading the XML rather than the compiled satellites.
/// </summary>
public sealed class ResxStringLocalizer(ResxCatalog catalog, ILogger<ResxStringLocalizer> logger) : IStringLocalizer
{
    public LocalizedString this[string name]
    {
        get
        {
            var value = Resolve(name);
            return new LocalizedString(name, value ?? name, resourceNotFound: value is null);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var value = Resolve(name);
            if (value is null)
                return new LocalizedString(name, name, resourceNotFound: true);

            return new LocalizedString(name, Format(name, value, arguments), resourceNotFound: false);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var suffix in SuffixChain(CultureInfo.CurrentUICulture))
        {
            foreach (var (key, value) in catalog.GetAll(suffix))
                if (seen.Add(key))
                    yield return new LocalizedString(key, value, resourceNotFound: false);

            if (!includeParentCultures) yield break;
        }
    }

    private string? Resolve(string name)
    {
        foreach (var suffix in SuffixChain(CultureInfo.CurrentUICulture))
            if (catalog.TryGet(suffix, name, out var value))
                return value;

        return null;
    }

    /// <summary>
    /// de-DE -> "de" -> neutral. English maps straight to the neutral file, which is the English
    /// copy, so it is never looked up twice.
    /// </summary>
    private static IEnumerable<string> SuffixChain(CultureInfo culture)
    {
        var language = culture.TwoLetterISOLanguageName;
        if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
            yield return language;

        yield return ResxCatalog.NeutralSuffix;
    }

    private string Format(string name, string value, object[] arguments)
    {
        try
        {
            return string.Format(CultureInfo.CurrentCulture, value, arguments);
        }
        catch (FormatException ex)
        {
            // Saves are validated for placeholder parity, so this should be unreachable — but these
            // files are now editable at runtime, and a broken placeholder must degrade to showing
            // the raw string rather than throwing on a public page.
            logger.LogError(ex, "Malformed format placeholders in translation {Key}", name);
            return value;
        }
    }
}
