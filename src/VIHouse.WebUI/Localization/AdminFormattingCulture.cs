using System.Collections.Concurrent;
using System.Globalization;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// The admin panel's formatting culture: English numbers and text rules, the interface language's
/// dates.
///
/// RouteCultureProvider keeps formatting English in the panel on purpose — a price typed as 12.50
/// stays twelve fifty (Turkish reads the dot as a thousands separator) and the Turkish dotless-i
/// rules never touch a slug or an email comparison. That also left every date in English
/// ("7 October 2026 tarihinden beri"). This keeps en-GB for numbers and casing and takes only the
/// month and day names from the language the admin chose. Posted dates are ISO
/// (datetime-local inputs), which parse the same in any culture.
/// </summary>
public static class AdminFormattingCulture
{
    private static readonly ConcurrentDictionary<string, CultureInfo> Cache = new();

    public static CultureInfo For(CultureInfo interfaceCulture) =>
        Cache.GetOrAdd(interfaceCulture.Name, _ =>
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo(SiteCultures.Default).Clone();
            culture.DateTimeFormat = (DateTimeFormatInfo)interfaceCulture.DateTimeFormat.Clone();
            return CultureInfo.ReadOnly(culture);
        });
}
