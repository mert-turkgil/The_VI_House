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
            email, CultureInfo.CurrentUICulture.Name, source: "coming-soon", consentText: loc["Notify.Consent"].Value, ct: ct);

        // POST-redirect-GET, so a refresh after signing up does not resubmit the address and a
        // rate-limit rejection is not what a reload produces.
        TempData["NotifyMessage"] = loc[key ?? "ComingSoon.Notify.Success"].Value;
        TempData["NotifyOk"] = key is null || key == "ComingSoon.Notify.Already";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>The "take me off the list" link in a launch email. Shows the question only — see
    /// Leave.cshtml for why the removal itself is a POST.</summary>
    [HttpGet("leave/{id:guid}")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        ViewData["Title"] = loc["ComingSoon.Leave.Title"];
        NoIndex();
        var signup = await signups.GetAsync(id, ct);
        return View(new LaunchListLeaveViewModel(id, signup?.Email, Removed: false));
    }

    [HttpPost("leave/{id:guid}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-submit")]
    public async Task<IActionResult> LeaveConfirmed(Guid id, CancellationToken ct)
    {
        ViewData["Title"] = loc["ComingSoon.Leave.DoneTitle"];
        NoIndex();
        await signups.LeaveAsync(id, ct);
        // Same page either way: whether the id was still on the list is not something to tell a
        // stranger holding a forwarded link.
        return View(nameof(Leave), new LaunchListLeaveViewModel(id, null, Removed: true));
    }

    /// <summary>A personal link out of an email — nothing a search engine should index.</summary>
    private void NoIndex()
    {
        this.SetSeo(loc["ComingSoon.MetaDescription"].Value, canonicalPath: "/coming-soon");
        if (ViewData["Seo"] is VIHouse.WebUI.ViewModels.Seo.PageSeo seo) seo.NoIndex = true;
    }
}

public record LaunchListLeaveViewModel(Guid Id, string? Email, bool Removed);
