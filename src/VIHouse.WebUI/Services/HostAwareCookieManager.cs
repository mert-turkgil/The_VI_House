using Microsoft.AspNetCore.Authentication.Cookies;

namespace VIHouse.WebUI.Services;

/// <summary>
/// Drops the auth cookie's <c>Domain</c> when the request's host could never match it, and writes a
/// host-only cookie instead.
///
/// The setting itself is legitimate: production serves thevihouse.com and admin.thevihouse.com, and
/// a shared ".thevihouse.com" cookie domain is what lets one sign-in cover both. The problem is the
/// failure mode when it does <em>not</em> match. A browser silently discards a cookie whose Domain
/// isn't a suffix of the host it's talking to — no console error, no server error. The sign-in
/// succeeds, the response looks correct, the cookie evaporates, and the next request is anonymous,
/// so the visitor lands back on the login page having been told nothing. It is the same symptom for
/// a typo in the domain as for a deliberate local test of a production build, and neither leaves a
/// trace to debug from.
///
/// Rather than let that happen, a mismatch degrades to a cookie scoped to the host actually being
/// used — which is exactly right for localhost, an IP, or a staging hostname — and says so in the
/// log once per host, at warning level, so a genuine misconfiguration in production is loud instead
/// of invisible.
///
/// Deletion goes through the same transformation: a cookie written without a Domain will not be
/// cleared by a delete that specifies one, so skipping this on the way out would break sign-out.
/// </summary>
public sealed class HostAwareCookieManager(ILoggerFactory loggerFactory) : ICookieManager
{
    private readonly ChunkingCookieManager _inner = new();
    private readonly ILogger _logger = loggerFactory.CreateLogger<HostAwareCookieManager>();

    /// <summary>Hosts already warned about, so a mismatch logs once rather than on every response.</summary>
    private readonly HashSet<string> _warned = new(StringComparer.OrdinalIgnoreCase);

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options) =>
        _inner.AppendResponseCookie(context, key, value, Adjust(context, options));

    public void DeleteCookie(HttpContext context, string key, CookieOptions options) =>
        _inner.DeleteCookie(context, key, Adjust(context, options));

    public string? GetRequestCookie(HttpContext context, string key) =>
        _inner.GetRequestCookie(context, key);

    private CookieOptions Adjust(HttpContext context, CookieOptions options)
    {
        var domain = options.Domain;
        var host = context.Request.Host.Host;

        if (string.IsNullOrEmpty(domain) || Matches(host, domain))
            return options;

        Warn(host, domain);

        // Copied rather than mutated: CookieOptions instances are reused across requests by the
        // authentication handler, so clearing Domain in place would permanently disable the shared
        // cookie domain for everyone after the first mismatched request.
        return new CookieOptions
        {
            Path = options.Path,
            Expires = options.Expires,
            Secure = options.Secure,
            HttpOnly = options.HttpOnly,
            SameSite = options.SameSite,
            MaxAge = options.MaxAge,
            IsEssential = options.IsEssential,
            Domain = null,
        };
    }

    /// <summary>
    /// A cookie domain covers the domain itself and anything under it: ".thevihouse.com" matches
    /// thevihouse.com and admin.thevihouse.com, but not localhost, an IP, or thevihouse.com.evil.tld.
    /// </summary>
    private static bool Matches(string host, string domain)
    {
        var bare = domain.TrimStart('.');

        return string.Equals(host, bare, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + bare, StringComparison.OrdinalIgnoreCase);
    }

    private void Warn(string host, string domain)
    {
        lock (_warned)
        {
            if (!_warned.Add(host)) return;
        }

        _logger.LogWarning(
            "CookieDomain is configured as \"{Domain}\", which cannot apply to requests for \"{Host}\". " +
            "Issuing a host-only cookie so sign-in still works. If this is production, the CookieDomain " +
            "setting is wrong for this deployment and single sign-on across subdomains will not happen.",
            domain, host);
    }
}
