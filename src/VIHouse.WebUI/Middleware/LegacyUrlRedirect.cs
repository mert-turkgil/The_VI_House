using System.Text.RegularExpressions;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Middleware;

/// <summary>
/// 301s the URLs the site used to have — /Identity/Account/…, /Admin/AdminUsers/Details/{id},
/// /Home/Index — to the ones it has now. They are gone from routing entirely (no conventional
/// routes remain, and the Identity pages carry only their clean selector), but links to them are
/// still out in the world: every password-reset and confirmation email sent before this change
/// points at /Identity/Account/ResetPassword?code=…, and admins have the old panel bookmarked.
///
/// Targets are resolved through LinkGenerator rather than a second table, so the route attributes
/// and IdentityPageRoutes stay the only place a URL is written down. The overloads that take no
/// HttpContext need no ambient route data, which is what lets this run before UseRouting. A path
/// that resolves to nothing falls through to the ordinary 404 page. GET and HEAD only — a POST to
/// an old URL has nothing sensible to be redirected to.
/// </summary>
public sealed partial class LegacyUrlRedirect(RequestDelegate next, LinkGenerator links)
{
    [GeneratedRegex(@"^/(?:(?<culture>[a-z]{2})/)?identity/(?<page>account(?:/[a-z0-9]+)*|error)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex IdentityPattern();

    [GeneratedRegex(@"^/admin(?:/(?<controller>admin[a-z]+)(?:/(?<action>[a-z0-9]+)(?:/(?<id>[^/]+))?)?)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex AdminPattern();

    [GeneratedRegex(@"^/(?:(?<culture>[a-z]{2})/)?home(?:/(?<action>index|privacy|error))?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex HomePattern();

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            var target = Resolve(context.Request.Path.Value ?? "/");
            if (target is not null)
            {
                context.Response.Redirect(target + context.Request.QueryString, permanent: true);
                return;
            }
        }

        await next(context);
    }

    private string? Resolve(string path)
    {
        if (IdentityPattern().Match(path) is { Success: true } identity)
        {
            var page = "/" + identity.Groups["page"].Value;
            var target = links.GetPathByPage(page, values: Values("Identity"))
                // "/Identity/Account/Manage" is the folder; the page is its Index.
                ?? links.GetPathByPage(page + "/Index", values: Values("Identity"));
            return WithCulture(target, CultureOf(identity));
        }

        if (AdminPattern().Match(path) is { Success: true } admin)
        {
            // The bare /admin is the new dashboard URL itself; only the old shapes are rewritten.
            if (!admin.Groups["controller"].Success) return null;

            var controller = admin.Groups["controller"].Value;
            var action = admin.Groups["action"].Success ? admin.Groups["action"].Value : "Index";
            var values = new RouteValueDictionary { ["area"] = "Admin" };
            if (admin.Groups["id"].Success) values["id"] = admin.Groups["id"].Value;

            return links.GetPathByAction(action, controller, values)
                ?? links.GetPathByAction("Index", controller, new RouteValueDictionary { ["area"] = "Admin" });
        }

        if (HomePattern().Match(path) is { Success: true } home)
        {
            var target = home.Groups["action"].Value.ToLowerInvariant() switch
            {
                "privacy" => links.GetPathByAction("Privacy", "Legal", Values("")),
                "error" => links.GetPathByAction("Index", "Error", new RouteValueDictionary { ["area"] = "", ["code"] = 500 }),
                _ => links.GetPathByAction("Index", "Home", Values("")),
            };
            return WithCulture(target, CultureOf(home));
        }

        return null;
    }

    private static string? CultureOf(Match match)
    {
        if (!match.Groups["culture"].Success) return null;
        var code = match.Groups["culture"].Value.ToLowerInvariant();
        return SiteCultures.UrlCodes.Contains(code) ? code : null;
    }

    private static RouteValueDictionary Values(string area) => new() { ["area"] = area };

    /// <summary>
    /// Re-applies the language prefix by hand. Handing the culture to LinkGenerator as a route value
    /// does not pick the {culture:sitelang} twin — the bare template also satisfies every required
    /// value, wins on order, and the culture ends up as ?culture=tr. Same rule CultureUrlHelper
    /// applies to every generated link: the prefix is a string operation on a clean path.
    /// </summary>
    private static string? WithCulture(string? path, string? culture) =>
        path is null || culture is null ? path : $"/{culture}{(path == "/" ? "" : path)}";
}
