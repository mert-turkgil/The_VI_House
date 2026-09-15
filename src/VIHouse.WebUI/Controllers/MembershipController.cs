using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Services;
using VIHouse.WebUI.ViewModels.Membership;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Controllers;

/// <summary>The public membership catalog + purchase flow (brief §44-46). Purchase requires being
/// logged in already: unlike an event-ticket purchase, there's no Application that already captured
/// the buyer's name/email, so there's no equivalent auto-provisioning step here — /join is the
/// anonymous route.</summary>
[Route("membership")]
public class MembershipController(
    IMembershipService membershipService,
    ISeminarService seminarService,
    IBookingRepository bookings,
    UserManager<ApplicationUser> userManager,
    IOptions<FeatureOptions> features,
    IStringLocalizer<SharedResource> loc) : Controller
{
    /// <summary>
    /// What membership is and how it is granted — and, for a member, what theirs is.
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
        this.SetSeo(loc["Seo.Membership.Description"].Value, canonicalPath: "/membership");

        var model = new MembershipPageViewModel
        {
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            SalesOpen = features.Value.MembershipSales,
            CommunityEnabled = features.Value.Community,
            DirectoryEnabled = features.Value.MemberDirectory,
        };

        if (features.Value.MembershipSales)
        {
            foreach (var plan in await membershipService.GetActivePlansAsync(ct))
            {
                var availability = plan.MaxMembers is null ? null : await membershipService.GetPlanAvailabilityAsync(plan.Id, ct);
                model.Plans.Add(MembershipPlanCardViewModel.FromEntity(plan, availability));
            }
        }

        if (model.IsAuthenticated && Guid.TryParse(userManager.GetUserId(User), out var userId))
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            model.FirstName = user?.FirstName;
            model.Current = await membershipService.GetMembershipSummaryAsync(userId, ct);
            model.CanManageBilling = model.Current is { HasProviderSubscription: true, Membership.ProviderCustomerId: not null };
            model.MemberNumber = model.Current is null ? null : $"VIH-{model.Current.Membership.Id:N}".Substring(0, 12).ToUpperInvariant();

            if (model.Current is null)
            {
                model.BookingCount = (await bookings.GetByUserAsync(userId, ct)).Count;
                model.SessionCount = (await seminarService.GetEnrolledSeminarsAsync(userId, ct)).Count;
            }
        }

        return View(model);
    }

    [Authorize]
    [HttpPost("checkout/{planId:guid}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Checkout(Guid planId, string? promoCode, CancellationToken ct)
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
        var result = await membershipService.InitiateCheckoutAsync(planId, userId, referralCode, promoCode, successUrl, cancelUrl, ct);
        if (!result.Success)
        {
            TempData["MembershipError"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        return Redirect(result.CheckoutUrl!);
    }

    /// <summary>
    /// A full plan's stand-in for checkout. Lands in the same table as the launch list (source
    /// "waitlist:{plan name}"), so admins see who is waiting for which plan on the page they already
    /// have, and the same address is never stored twice.
    /// </summary>
    [HttpPost("waitlist/{planId:guid}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("application-submit")]
    public async Task<IActionResult> Waitlist(Guid planId, string? email, [FromServices] INotifySignupService signups, CancellationToken ct)
    {
        if (!features.Value.MembershipSales) return NotFound();

        var plan = await membershipService.GetPlanAsync(planId, ct);
        if (plan is null) return NotFound();

        // A signed-in visitor's own address, always — the form field is only shown to guests.
        if (User.Identity?.IsAuthenticated == true && await userManager.GetUserAsync(User) is { } user)
            email = user.Email;

        var key = await signups.SubscribeAsync(email, CultureInfo.CurrentUICulture.Name, source: $"waitlist:{plan.Name}"[..Math.Min(50, 9 + plan.Name.Length)], ct);

        TempData["WaitlistMessage"] = loc[key ?? "Membership.Waitlist.Thanks"].Value;
        TempData["WaitlistOk"] = key is null || key == "ComingSoon.Notify.Already";
        return Redirect(Url.Action(nameof(Index), "Membership") + "#membership-plans");
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
