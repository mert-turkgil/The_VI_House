using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// The page every visitor sees while <c>Features:ComingSoon</c> is on — see
/// <see cref="Middleware.ComingSoonGate"/>, which is what puts them here.
///
/// Reachable even when the flag is off. That is deliberate: it makes the page previewable and
/// testable without closing the site, and there is nothing on it worth hiding.
/// </summary>
[Route("coming-soon")]
[AllowAnonymous]
public class ComingSoonController(INotifySignupService signups, IStringLocalizer<SharedResource> loc) : Controller
{
    /// <summary>
    /// Deliberately no [ResponseCache]. SeoController caches its responses and it would be a
    /// natural thing to copy here, but this page carries an antiforgery token in a form: cached
    /// HTML would hand one visitor's token to everyone else and every sign-up would fail with a 400.
    /// </summary>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = loc["ComingSoon.Title"];
        this.SetSeo(loc["ComingSoon.MetaDescription"].Value, canonicalPath: "/coming-soon");
        return View();
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-submit")]
    public async Task<IActionResult> Notify(string? email, CancellationToken ct)
    {
        // The culture is taken from the request rather than the form: it is what they were reading
        // when they decided to sign up, and it is the language the launch announcement should be in.
        var key = await signups.SubscribeAsync(
            email, CultureInfo.CurrentUICulture.Name, source: "coming-soon", ct);

        // POST-redirect-GET, so a refresh after signing up does not resubmit the address and a
        // rate-limit rejection is not what a reload produces.
        TempData["NotifyMessage"] = loc[key ?? "ComingSoon.Notify.Success"].Value;
        TempData["NotifyOk"] = key is null || key == "ComingSoon.Notify.Already";

        return RedirectToAction(nameof(Index));
    }
}
