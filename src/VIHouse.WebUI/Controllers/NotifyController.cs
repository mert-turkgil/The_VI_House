using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.ViewModels.Shared;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// "Tell me when it opens" on an empty Experiences, Sessions or Membership page. The address goes
/// on the same Launch List as the Coming Soon sign-ups, tagged "empty:{topic}", so Admin > Launch
/// List shows who is waiting for what and can write to them when it opens.
/// </summary>
[Route("notify")]
public class NotifyController(
    INotifySignupService signups,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpPost("{topic}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-submit")]
    public async Task<IActionResult> Subscribe(string topic, string? email, string? returnUrl, CancellationToken ct)
    {
        // "news" is the footer's list; the others are the empty Experiences/Sessions/Membership pages.
        var isFooter = topic == "news";
        if (!isFooter && !EmptyStageViewModel.IsTopic(topic)) return NotFound();

        // A signed-in visitor is signed up with their account address; the form only shows them a button.
        if (User.Identity?.IsAuthenticated == true && await userManager.GetUserAsync(User) is { Email: { } own })
            email = own;

        var key = await signups.SubscribeAsync(email, CultureInfo.CurrentUICulture.Name, source: isFooter ? "footer" : $"empty:{topic}", consentText: loc["Notify.Consent"].Value, ct: ct);

        TempData["EmptyStageTopic"] = topic;
        TempData["EmptyStageMessage"] = loc[key ?? "EmptyStage.Notify.Thanks"].Value;
        TempData["EmptyStageOk"] = key is null || key == "ComingSoon.Notify.Already";

        var back = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        return Redirect(back + (isFooter ? "#site-newsletter" : "#empty-stage"));
    }
}
