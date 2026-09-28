using System.Numerics;
using System.Text.RegularExpressions;

namespace VIHouse.Business.Concrete;

/// <summary>
/// IBAN normalising and checking (ISO 13616): country code, check digits, then the mod-97 test.
/// Catches the typos that would otherwise send a commission payout to nobody — a transposed
/// digit fails the checksum. It cannot tell whether the account belongs to the person.
/// </summary>
public static partial class Iban
{
    public static string Normalize(string? value) =>
        new string((value ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static bool IsValid(string? value)
    {
        var iban = Normalize(value);
        if (iban.Length is < 15 or > 34 || !Shape().IsMatch(iban)) return false;

        var rearranged = iban[4..] + iban[..4];
        var digits = string.Concat(rearranged.Select(c => char.IsDigit(c) ? c.ToString() : (c - 'A' + 10).ToString()));
        return BigInteger.Parse(digits) % 97 == 1;
    }

    /// <summary>"DE89 3704 0044 0532 0130 00" — how people read one back.</summary>
    public static string Format(string? value) =>
        string.Join(" ", Enumerable.Range(0, (Normalize(value).Length + 3) / 4)
            .Select(i => Normalize(value).Substring(i * 4, Math.Min(4, Normalize(value).Length - i * 4))));

    /// <summary>"•••• 3000" — enough to recognise the account without handing it out.</summary>
    public static string Mask(string? value)
    {
        var iban = Normalize(value);
        return iban.Length < 8 ? "••••" : $"{iban[..2]}•• •••• {iban[^4..]}";
    }

    public static bool IsValidBic(string? value) =>
        string.IsNullOrWhiteSpace(value) || Bic().IsMatch(Normalize(value));

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]+$")]
    private static partial Regex Shape();

    [GeneratedRegex("^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex Bic();
}
