namespace VIHouse.WebUI.Helpers;

/// <summary>Formats integer-minor-unit money (brief §184) for display — never do this math inline in a view.</summary>
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
        var symbol = Symbols.GetValueOrDefault(currency.ToUpperInvariant(), currency.ToUpperInvariant() + " ");
        var major = priceMinor / 100m;
        return $"{symbol}{major:N0}";
    }
}
