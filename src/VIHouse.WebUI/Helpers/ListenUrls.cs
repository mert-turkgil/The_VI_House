namespace VIHouse.WebUI.Helpers;

/// <summary>
/// Turns the addresses Kestrel listens on ("https://localhost:7054;http://localhost:5062",
/// "http://+:80") into a base URL that works as a link in an email.
///
/// Used only when Site:BaseUrl is empty. Production sets it explicitly (and must — a base URL taken
/// from the request's Host header would let anyone who can send a request choose where password-reset
/// links point). Locally it was a hard-coded "http://localhost:5252" that stopped matching the port
/// launchSettings actually uses, so every link in every email pointed at nothing.
/// </summary>
public static class ListenUrls
{
    public static string? ToBaseUrl(string? urls)
    {
        if (string.IsNullOrWhiteSpace(urls)) return null;

        var candidates = urls
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalise)
            .Where(u => u is not null)
            .Cast<Uri>()
            .ToList();

        // https when there is one: it is what a browser would be redirected to anyway.
        var chosen = candidates.FirstOrDefault(u => u.Scheme == Uri.UriSchemeHttps) ?? candidates.FirstOrDefault();
        return chosen?.GetLeftPart(UriPartial.Authority);
    }

    /// <summary>Wildcard binds ("+", "*", "0.0.0.0", "[::]") are listen instructions, not hosts a
    /// reader's browser can open, so they are shown as localhost.</summary>
    private static Uri? Normalise(string url)
    {
        var replaced = url
            .Replace("://+", "://localhost")
            .Replace("://*", "://localhost")
            .Replace("://0.0.0.0", "://localhost")
            .Replace("://[::]", "://localhost");

        return Uri.TryCreate(replaced, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }
}
