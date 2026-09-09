using Microsoft.AspNetCore.Localization;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// The language comes from the URL, and only from the URL.
///
/// A prefixed path is that language; an unprefixed path is English. There is no third case, and
/// deliberately no cookie in the chain — which is a change worth stating plainly, because the site
/// used to work the other way round.
///
/// The reason is the thing that was actually broken. With a cookie in play, /experiences meant
/// "English" in the sitemap, in the canonical tag and in hreflang, but rendered German to anyone
/// whose cookie said German. So the one URL the site published as its English page did not reliably
/// serve English, and a link sent to a colleague opened in *their* last language rather than the
/// one it was written in. Every page now has a URL per language, so the URL can simply be believed.
///
/// The cookie is still written by the language switcher — see CultureController, which uses it to
/// send a returning visitor to their language from the site root. It just no longer overrides an
/// explicit instruction.
/// </summary>
public class RouteCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        // RouteData is populated by the routing middleware, which runs before localization only
        // because UseRouting is ordered first in Program.cs. Parsing the path here instead would
        // mean two places disagreeing about what counts as a language segment.
        var code = httpContext.GetRouteValue("culture") as string;
        var culture = SiteCultures.FromUrlCode(code) ?? SiteCultures.Default;

        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }
}
