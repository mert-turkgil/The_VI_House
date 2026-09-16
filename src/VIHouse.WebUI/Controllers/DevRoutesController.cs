using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace VIHouse.WebUI.Controllers;

/// <summary>Development only: every routable endpoint with its HTTP methods, as the router sees
/// them. The quickest way to answer "why is this a 405 / 404" without attaching a debugger.</summary>
[AllowAnonymous]
[Route("dev/routes")]
public class DevRoutesController(EndpointDataSource endpoints, IWebHostEnvironment env) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        if (!env.IsDevelopment()) return NotFound();

        var lines = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new
            {
                Pattern = e.RoutePattern.RawText ?? "",
                Methods = string.Join(",", e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"]),
                e.DisplayName,
                e.Order,
            })
            .OrderBy(e => e.Pattern).ThenBy(e => e.Methods)
            .Select(e => $"{e.Methods,-12} {e.Order,3}  /{e.Pattern,-60} {e.DisplayName}");

        return Content(string.Join("\n", lines), "text/plain; charset=utf-8");
    }
}
