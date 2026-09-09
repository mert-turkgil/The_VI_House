using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// Gives every public page a second, language-prefixed URL: /experiences and /de/experiences are
/// the same action, in two languages.
///
/// This is the foundation the whole SEO layer stands on, and without it none of the rest is worth
/// building. The site previously chose its language from a cookie alone, which meant:
///
///   - Googlebot sends no cookie, so it only ever saw English. Three of the four languages were
///     invisible to search entirely — not ranked badly, absent.
///   - A link pasted into a chat opened in the *recipient's* language, not the sender's. Sending
///     someone the Turkish page was impossible; there was no URL that meant "the Turkish page".
///   - hreflang could not be expressed at all, because it annotates alternate URLs and there was
///     exactly one URL.
///
/// English stays unprefixed so every existing link, bookmark and backlink keeps working, and so
/// there is a natural x-default. The cookie still works and still records a preference — it simply
/// stops being the only thing that decides.
/// </summary>
public class CulturePrefixConvention : IApplicationModelConvention
{
    /// <summary>
    /// The prefix goes on the *controller's* route, not the action's.
    ///
    /// This is the whole subtlety of the class. MVC builds a route by combining each controller
    /// selector with each action selector, so prefixing the action template on
    /// <c>[Route("experiences")] + [HttpGet("")]</c> yields "experiences/{culture}" — the language
    /// on the wrong end, giving /experiences/de. Prefixing the controller selector yields
    /// "{culture}/experiences", which is what was wanted.
    ///
    /// Areas are excluded wholesale: Admin is an internal tool in one language behind a login, and
    /// four URLs each would multiply its surface for no reader.
    /// </summary>
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            if (IsExcluded(controller)) continue;

            foreach (var selector in controller.Selectors.ToList())
            {
                if (selector.AttributeRouteModel?.Template is not { } template) continue;

                // A second selector rather than an optional segment: an optional parameter in the
                // middle of a template does not round-trip in ASP.NET Core routing — it matches,
                // but link generation drops the rest of the path. Two concrete templates both
                // match and both generate.
                controller.Selectors.Add(new SelectorModel(selector)
                {
                    AttributeRouteModel = new AttributeRouteModel
                    {
                        Template = AttributeRouteModel.CombineTemplates("{culture:sitelang}", template),
                        Name = selector.AttributeRouteModel.Name is { } n ? n + "__culture" : null,
                    },
                });
            }
        }
    }

    private static bool IsExcluded(ControllerModel controller)
    {
        if (controller.RouteValues.TryGetValue("area", out var area) && !string.IsNullOrEmpty(area))
            return true;

        // Machine endpoints, not pages. A payment provider posting to /de/webhooks/stripe or a
        // crawler fetching /tr/sitemap.xml would be meaningless, and in the webhook's case the
        // duplicate route is extra attack surface for no gain. The SEO files describe every
        // language at once, so a per-language copy of each would be the same document four times.
        return controller.ControllerName is "Webhooks" or "Media" or "Culture" or "Seo";
    }
}

/// <summary>
/// Matches only the language codes the site actually serves, and only the prefixed ones — "en" is
/// deliberately absent, because English lives at the unprefixed URL and /en/experiences would be a
/// second URL for identical content, which is the duplicate-content problem canonical tags exist
/// to clean up after. Better not to create it.
/// </summary>
public class SiteLanguageRouteConstraint : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext, IRouter? route, string routeKey,
        RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var value) || value is null) return false;

        var code = Convert.ToString(value);
        return SiteCultures.UrlCodes.Contains(code, StringComparer.OrdinalIgnoreCase);
    }
}
