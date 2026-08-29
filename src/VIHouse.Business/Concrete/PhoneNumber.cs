namespace VIHouse.Business.Concrete;

/// <summary>
/// Turns what somebody typed into a form into something an SMS gateway will accept — or into nothing.
///
/// Application.Phone is free text and arrives in every shape a person writes a phone number in:
/// "+44 7700 900123", "07700 900123", "0090 555 123 45 67", "(555) 123-4567". A gateway wants E.164
/// and nothing else.
/// </summary>
public static class PhoneNumber
{
    /// <param name="defaultCountryCode">Dialling code to assume for a number typed without one, e.g.
    /// "+90". Without it a national number returns null rather than being guessed at.</param>
    /// <returns>"+" followed by 8–15 digits, or null when the input cannot be trusted to be a number.</returns>
    public static string? TryNormalise(string? raw, string? defaultCountryCode = null)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();
        var international = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0) return null;

        // 00 is how most of Europe writes what the rest of the world writes as +.
        if (!international && digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
            international = true;
        }

        if (!international)
        {
            var code = new string((defaultCountryCode ?? "").Where(char.IsAsciiDigit).ToArray());
            if (code.Length == 0) return null; // no way to know which country — see SmsOptions

            // The leading 0 of a national number is a trunk prefix, dropped the moment a country
            // code goes in front of it: 07700… in the UK is +447700…, not +4407700….
            digits = code + digits.TrimStart('0');
        }

        // E.164 tops out at 15 digits including the country code, and nothing real is shorter than 8.
        // Outside that it is a typo, and the gateway would only reject it more slowly and for money.
        return digits.Length is >= 8 and <= 15 ? "+" + digits : null;
    }
}
