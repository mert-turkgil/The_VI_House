using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace VIHouse.WebUI.Routing;

/// <summary>
/// Gives every Identity page its public URL from <see cref="IdentityPageRoutes"/> instead of the
/// framework's /Identity/Account/Manage/ChangePassword. The framework's selectors are replaced, not
/// supplemented — an Index page gets two of them (the page and its folder) and both would otherwise
/// stay reachable. Must be registered before CulturePageRouteConvention, which builds the
/// /{culture}/… twin from whatever selector it finds; the other way round the twin is built from the
/// old template and then cleared here, and /tr/login is a 404.
///
/// Link generation is unaffected: Url.Page, asp-page and RedirectToPage resolve by page name, and the
/// page name (RouteValues["page"]) is not touched.
/// </summary>
public sealed class IdentityCleanRouteConvention : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        if (!string.Equals(model.AreaName, "Identity", StringComparison.OrdinalIgnoreCase)) return;

        if (!IdentityPageRoutes.Map.TryGetValue(model.ViewEnginePath, out var path))
        {
            throw new InvalidOperationException(
                $"Identity page '{model.ViewEnginePath}' has no public URL in IdentityPageRoutes.Map — add one rather than let it fall back to /Identity/….");
        }

        model.Selectors.Clear();
        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = path.TrimStart('/') },
        });
    }
}
