using System.Text;

namespace VIHouse.WebUI.Helpers;

public static class AuthenticatorKey
{
    /// <summary>The shared key in groups of four, lower case — how people type it into an app.</summary>
    public static string Format(string unformattedKey)
    {
        var result = new StringBuilder();
        var position = 0;
        while (position + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(position, 4)).Append(' ');
            position += 4;
        }
        if (position < unformattedKey.Length)
            result.Append(unformattedKey.AsSpan(position));

        return result.ToString().ToLowerInvariant();
    }
}
