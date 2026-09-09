using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using VIHouse.Business.Options;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// Keeps generated links inside the language the reader is already in.
///
/// Without this, every internal link on a prefixed page pointed back at the English URL. A visitor
/// reading /tr clicked "Deneyimler" and landed on /experiences in English — the site quietly
/// dropped them out of their language on the first click, which made the whole language-prefix
/// scheme look broken from the visitor's side even though every URL resolved.
///
/// The cause is in link generation, not in the routes. CulturePrefixConvention gives each public
/// controller two attribute routes — "experiences" and "{culture:sitelang}/experiences" — and both
/// carry the same controller/action required values. The link generator therefore treats them as
/// equally good matches for Url.Action("Index", "Experiences"), picks the first, and puts any
/// leftover route value on the query string: passing culture explicitly produced
/// "/experiences?culture=tr" rather than "/tr/experiences". No amount of asp-route-culture on
/// individual links fixes that, and there are well over a hundred of them.
///
/// So the prefix is applied after generation instead, in exactly one place, using the same
/// SeoResolver.PathFor that builds the canonical tags and the sitemap. One function decides what a
/// URL looks like in a given language, and the head, the sitemap and every anchor on the page
/// cannot disagree about it.
/// </summary>
public sealed class CultureUrlHelperFactory(IUrlHelperFactory inner) : IUrlHelperFactory
{
    public IUrlHelper GetUrlHelper(ActionContext context)
    {
        var helper = inner.GetUrlHelper(context);
        return new CultureUrlHelper(helper);
    }
}

public sealed class CultureUrlHelper(IUrlHelper inner) : IUrlHelper
{
    /// <summary>
    /// Paths that must never be prefixed.
    ///
    /// These mirror CulturePrefixConvention.IsExcluded plus the static roots. Everything here has
    /// exactly one URL by design — the admin panel, the Identity pages (Razor Pages, which the
    /// convention never touched, so a prefixed URL would 404), the media routes, the crawler files
    /// and the built assets. Prefixing any of them would turn a working link into a 404, which is
    /// far worse than the problem this class exists to solve.
    /// </summary>
    private static readonly string[] Excluded =
    [
        "/admin", "/identity", "/media", "/culture", "/webhooks",
        "/sitemap.xml", "/robots.txt", "/llms.txt",
        "/dist", "/img", "/lib", "/icons", "/css", "/js",
        "/favicon.ico", "/sw.js", "/manifest.json",
    ];

    public ActionContext ActionContext => inner.ActionContext;

    public string? Action(UrlActionContext actionContext) => Localise(inner.Action(actionContext));

    public string? RouteUrl(UrlRouteContext routeContext) => Localise(inner.RouteUrl(routeContext));

    /// <summary>
    /// Deliberately not localised. Content resolves "~/dist/main.css" and friends — static files
    /// that exist at one path only.
    /// </summary>
    public string? Content(string? contentPath) => inner.Content(contentPath);

    public bool IsLocalUrl(string? url) => inner.IsLocalUrl(url);

    public string? Link(string? routeName, object? values) => inner.Link(routeName, values);

    /// <summary>
    /// Public and static because views need it too, not only this decorator.
    ///
    /// The decorator can only reach URLs the framework generates. A URL typed into the CMS — a hero
    /// button's target, an ecosystem card's link — is rendered straight into an href and never
    /// passes through IUrlHelper at all, so it kept its unprefixed form and sent a Turkish reader
    /// to the English page. Those call sites pass the value through here instead.
    ///
    /// Uses no instance state: the culture comes from the request and everything else is static.
    /// </summary>
    public static string? Localise(string? url)
    {
        if (string.IsNullOrEmpty(url)) return url;

        // Only site-relative paths. "//host/x" is protocol-relative and off-site, and anything with
        // a scheme was asked for absolutely on purpose.
        if (url[0] != '/' || url.StartsWith("//", StringComparison.Ordinal)) return url;

        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        var code = SiteCultures.ToUrlCode(culture);

        // Null for the default culture, which is served unprefixed — nothing to do.
        if (code is null) return url;

        var path = url;
        var suffix = "";
        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            suffix = path[cut..];
            path = path[..cut];
        }

        if (Excluded.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)
                              || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
        {
            return url;
        }

        // Already prefixed — by SeoResolver, by a hand-written href, or by this method on a value
        // that passed through twice. Prefixing again would produce /tr/tr/experiences.
        if (SeoResolver.StripCulture(path) != NormalisedPath(path)) return url;

        return SeoResolver.PathFor(culture, path) + suffix;
    }

    private static string NormalisedPath(string path) => "/" + path.TrimStart('/');
}
