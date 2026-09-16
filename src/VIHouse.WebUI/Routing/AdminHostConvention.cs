using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Routing;

namespace VIHouse.WebUI.Routing;

/// <summary>
/// Pins the admin area to its own host (admin.thevihouse.com) now that the area is attribute-routed.
/// RequireHost on a MapControllerRoute builder only ever decorated that conventional route's
/// endpoints; with attribute routes the equivalent is host metadata on the controller itself.
/// HostAttribute is IHostMetadata — the exact thing RequireHost writes — and metadata on a
/// controller selector is copied onto every action endpoint, the same way [Authorize] on
/// AdminControllerBase reaches each action. Not registered at all when AdminHost is unset
/// (Development), so localhost stays unrestricted.
/// </summary>
public sealed class AdminHostConvention(string[] hosts) : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        if (!controller.RouteValues.TryGetValue("area", out var area)
            || !string.Equals(area, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (var selector in controller.Selectors)
        {
            selector.EndpointMetadata.Add(new HostAttribute(hosts));
        }
    }
}
