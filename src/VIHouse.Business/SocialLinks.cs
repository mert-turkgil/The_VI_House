using VIHouse.Entities.Referrals;

namespace VIHouse.Business;

/// <summary>
/// Reading social-network addresses: which network a link points at, and the short handle to show
/// for it. Shared by the influencer profile, the author box and the link cards in articles, so a
/// link is named the same way everywhere.
/// </summary>
public static class SocialLinks
{
    private static readonly (string Host, SocialPlatform Platform)[] Hosts =
    [
        ("instagram.com", SocialPlatform.Instagram),
        ("tiktok.com", SocialPlatform.TikTok),
        ("youtube.com", SocialPlatform.YouTube),
        ("youtu.be", SocialPlatform.YouTube),
        ("x.com", SocialPlatform.X),
        ("twitter.com", SocialPlatform.X),
        ("linkedin.com", SocialPlatform.LinkedIn),
        ("facebook.com", SocialPlatform.Facebook),
        ("fb.com", SocialPlatform.Facebook),
    ];

    /// <summary>The network an http(s) address belongs to, or null for anything else.</summary>
    public static SocialPlatform? Detect(string? url)
    {
        if (!TryParse(url, out var uri)) return null;
        var host = uri.Host.ToLowerInvariant();
        foreach (var (known, platform) in Hosts)
            if (host == known || host.EndsWith("." + known, StringComparison.Ordinal)) return platform;
        return null;
    }

    /// <summary>
    /// "@name" for a profile on a known network (instagram.com/name, youtube.com/@name,
    /// tiktok.com/@name, x.com/name), "name" for linkedin.com/in/name, otherwise the bare host.
    /// </summary>
    public static string Handle(string? url)
    {
        if (!TryParse(url, out var uri)) return url ?? "";
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return host;

        return Detect(url) switch
        {
            SocialPlatform.LinkedIn when segments is ["in" or "company", var name, ..] => name,
            SocialPlatform.Instagram or SocialPlatform.TikTok or SocialPlatform.YouTube or SocialPlatform.X
                when segments.Length == 1 || segments[0].StartsWith('@') => "@" + segments[0].TrimStart('@'),
            _ => host,
        };
    }

    private static bool TryParse(string? url, out Uri uri)
    {
        uri = null!;
        return !string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri!)
            && uri.Scheme is "https" or "http";
    }
}
