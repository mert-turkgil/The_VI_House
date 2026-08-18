using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.ViewModels.Membership;

namespace VIHouse.WebUI.Controllers;

/// <summary>The public membership catalog + purchase flow (brief §44-46) — finally real content for
/// the "/membership" nav link that's been a dead placeholder since the first milestone. Purchase
/// requires being logged in already: unlike an event-ticket purchase, there's no Application that
/// already captured the buyer's name/email, so there's no equivalent auto-provisioning step here.</summary>
[Route("membership")]
public class MembershipController(
    IMembershipService membershipService,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var plans = await membershipService.GetActivePlansAsync(ct);
        ViewData["Title"] = loc["Membership.Title"];
        return View(plans.Select(MembershipPlanCardViewModel.FromEntity).ToList());
    }

    [Authorize]
    [HttpPost("checkout/{planId:guid}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Checkout(Guid planId, CancellationToken ct)
    {
        var userId = Guid.Parse(userManager.GetUserId(User)!);

        var successUrl = Url.Action(nameof(Success), "Membership", null, Request.Scheme)!;
        successUrl += (successUrl.Contains('?') ? "&" : "?") + "session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = Url.Action(nameof(Cancel), "Membership", null, Request.Scheme)!;

        var result = await membershipService.InitiateCheckoutAsync(planId, userId, successUrl, cancelUrl, ct);
        if (!result.Success)
        {
            TempData["MembershipError"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        return Redirect(result.CheckoutUrl!);
    }

    [HttpGet("success")]
    public async Task<IActionResult> Success([FromQuery(Name = "session_id")] string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return NotFound();

        var info = await membershipService.GetConfirmationBySessionAsync(sessionId, ct);
        if (info is null) return NotFound();

        ViewData["Title"] = info.IsConfirmed ? loc["Membership.Confirmed"] : loc["Membership.Processing"];
        return View(info);
    }

    [HttpGet("cancel")]
    public IActionResult Cancel()
    {
        ViewData["Title"] = loc["Membership.CancelledTitle"];
        return View();
    }
}
