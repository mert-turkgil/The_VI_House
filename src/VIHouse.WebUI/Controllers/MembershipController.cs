using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Services;
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
    IOptions<FeatureOptions> features,
    IStringLocalizer<SharedResource> loc) : Controller
{
    /// <summary>
    /// What membership is and how it is granted.
    ///
    /// While <see cref="FeatureOptions.MembershipSales"/> is off — the brief's Phase 1 — the page
    /// carries no plan cards and no checkout, because there is one way into the House and it starts
    /// with an application (§25). The plans are still loaded when sales are open, so switching the
    /// flag restores the storefront without touching this code.
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = loc["Membership.Title"];

        var plans = features.Value.MembershipSales
            ? (await membershipService.GetActivePlansAsync(ct)).Select(MembershipPlanCardViewModel.FromEntity).ToList()
            : [];

        return View(plans);
    }

    [Authorize]
    [HttpPost("checkout/{planId:guid}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Checkout(Guid planId, CancellationToken ct)
    {
        // Closed alongside the plan cards. A form that is not rendered can still be posted, and a
        // subscription bought while the House is application-only would be a real charge for a
        // product nobody meant to sell.
        if (!features.Value.MembershipSales) return NotFound();

        var userId = Guid.Parse(userManager.GetUserId(User)!);

        var successUrl = Url.Action(nameof(Success), "Membership", null, Request.Scheme)!;
        successUrl += (successUrl.Contains('?') ? "&" : "?") + "session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = Url.Action(nameof(Cancel), "Membership", null, Request.Scheme)!;

        var referralCode = Request.Cookies[ReferralCookie.Name];
        var result = await membershipService.InitiateCheckoutAsync(planId, userId, referralCode, successUrl, cancelUrl, ct);
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
