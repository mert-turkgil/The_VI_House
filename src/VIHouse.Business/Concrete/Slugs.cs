namespace VIHouse.Business.Concrete;

/// <summary>
/// Turns whatever an editor typed (or the title, when they typed nothing) into a URL-safe slug.
/// Deliberately conservative: ASCII letters, digits and single hyphens, because the slug ends up in
/// a route, in emails and in links people paste, and a Turkish "ı" or a German "ß" surviving into a
/// URL is a support ticket waiting to happen. The title keeps the real characters; only the slug is
/// flattened. Shared by sessions and journal posts so the two can never disagree.
/// </summary>
public static class Slugs
{
    public static string From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("ş", "s").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ö", "o").Replace("ç", "c").Replace("ä", "ae").Replace("ß", "ss")
            .Replace("õ", "o").Replace("â", "a").Replace("î", "i").Replace("û", "u");

        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsAsciiLetterOrDigit(ch)) builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }

        var slug = builder.ToString().Trim('-');
        return slug.Length > 180 ? slug[..180].TrimEnd('-') : slug;
    }
}
