using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.WebUI.Models;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// The pages behind UseStatusCodePagesWithReExecute("/error/{0}") and UseExceptionHandler("/error/500").
/// A mistyped URL used to come back as a bare status with no body at all; an unhandled exception
/// rendered the MVC scaffold's "Error." page. Both now get the House's own page, in its chrome,
/// with a way back.
///
/// Anonymous on purpose: a 404 for a signed-out visitor must not turn into a login redirect. The
/// status code is re-asserted here because the re-execute pipeline runs a fresh request whose
/// default is 200, and a 404 that answers 200 is what search engines index as a real page.
/// </summary>
[AllowAnonymous]
[Route("error")]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorController : Controller
{
    [HttpGet("{code:int}")]
    public IActionResult Index(int code)
    {
        Response.StatusCode = code is >= 400 and < 600 ? code : 500;

        if (code == 404)
        {
            ViewData["Title"] = "Page not found";
            return View("NotFound");
        }

        ViewData["Title"] = "Something went wrong";
        return View("Error", new ErrorViewModel
        {
            // Only a server fault has a trace worth quoting to support; a 403 or 405 does not.
            RequestId = code >= 500 ? Activity.Current?.Id ?? HttpContext.TraceIdentifier : null,
        });
    }
}
