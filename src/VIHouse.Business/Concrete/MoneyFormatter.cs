namespace VIHouse.Business.Concrete;

/// <summary>
/// Formats integer-minor-unit money (brief §184) for people to read — pages, emails and
/// notifications alike, so an amount looks the same wherever it appears. Never do this math inline.
/// Whole amounts drop the pence ("£1,499"); anything else keeps them ("£33.30"), so a commission
/// or a partial refund is never silently rounded.
/// </summary>
public static class MoneyFormatter
{
    private static readonly Dictionary<string, string> Symbols = new()
    {
        ["EUR"] = "€",
        ["GBP"] = "£",
        ["USD"] = "$",
        ["CHF"] = "CHF ",
    };

    public static string Format(long priceMinor, string currency)
    {
        var code = currency.ToUpperInvariant();
        var symbol = Symbols.GetValueOrDefault(code, code + " ");
        var major = priceMinor / 100m;
        return priceMinor % 100 == 0 ? $"{symbol}{major:N0}" : $"{symbol}{major:N2}";
    }
}
