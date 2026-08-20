using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Experiences;
using VIHouse.WebUI.Services;
using VIHouse.WebUI.ViewModels.Experiences;
using VIHouse.WebUI.ViewModels.Membership;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Join-and-pay for someone with no account: fill the form, choose a plan, pay, and the account is
/// created for you. Anonymous by design — this is the front door.
///
/// Distinct from /apply, which is the route into a single <em>experience</em> and goes through
/// admin review before any money is taken. This one is the direct membership purchase: no review
/// step, so nothing here may create a usable account on its own — the account only becomes real
/// once Stripe confirms payment, and stays behind the onboarding gate until the member proves their
/// email and switches on two-factor.
/// </summary>
[Route("join")]
public class JoinController(
    IMembershipService membershipService,
    IExperienceService experienceService,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? plan, CancellationToken ct)
    {
        // Already signed in? The logged-in purchase path already exists and knows who they are.
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Membership");

        var form = new JoinFormViewModel
        {
            Plans = await membershipService.GetActivePlansAsync(ct),
            PlanId = plan ?? Guid.Empty,
            OpenExperiences = await LoadOpenExperiencesAsync(ct),
        };

        if (Request.Cookies.TryGetValue(ReferralCookie.Name, out var referral))
            form.ReferralCode = referral;

        ViewData["Title"] = "Join The VI House";
        return View(form);
    }

    /// <summary>
    /// The experiences a visitor could apply to instead of subscribing. Shown alongside the plans so
    /// the two routes into the House are presented together — they lead to genuinely different
    /// journeys (a membership is bought outright; a single event is applied for and reviewed by
    /// hand), and the page says so rather than implying they're interchangeable.
    /// </summary>
    private async Task<List<ExperienceCardViewModel>> LoadOpenExperiencesAsync(CancellationToken ct)
    {
        var open = await experienceService.GetPublicListingAsync(
            new ExperienceFilter { Status = ExperienceStatus.ApplicationsOpen, Take = 6 }, ct);

        return open.Select(ExperienceCardViewModel.FromEntity).ToList();
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Index(JoinFormViewModel form, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Membership");

        if (!form.AgreeToTerms)
            ModelState.AddModelError(nameof(form.AgreeToTerms), "You must agree to the Terms & Conditions and Privacy Policy to join.");

        if (form.PlanId == Guid.Empty)
            ModelState.AddModelError(nameof(form.PlanId), "Choose a plan to continue.");

        if (!ModelState.IsValid)
        {
            form.Plans = await membershipService.GetActivePlansAsync(ct);
            ViewData["Title"] = "Join The VI House";
            return View(form);
        }

        var successUrl = Url.Action(nameof(Success), "Join", null, Request.Scheme)!;
        successUrl += (successUrl.Contains('?') ? "&" : "?") + "session_id={CHECKOUT_SESSION_ID}";
        var cancelUrl = Url.Action(nameof(Index), "Join", null, Request.Scheme)!;

        var result = await membershipService.InitiateJoinCheckoutAsync(
            new JoinRequest(
                form.PlanId,
                form.FirstName.Trim(),
                form.LastName.Trim(),
                form.Email.Trim(),
                form.Country.Trim().ToUpperInvariant(),
                form.City?.Trim(),
                form.ReferralCode),
            successUrl, cancelUrl, ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            form.Plans = await membershipService.GetActivePlansAsync(ct);
            ViewData["Title"] = "Join The VI House";
            return View(form);
        }

        return Redirect(result.CheckoutUrl!);
    }

    /// <summary>
    /// Where Stripe sends the browser back to. Reads local state only — the webhook, not this
    /// redirect, is what actually confirms the payment, so a visitor who closes the tab here still
    /// gets their account, and one who forges a session id gets nothing.
    /// </summary>
    [HttpGet("welcome")]
    public async Task<IActionResult> Success([FromQuery(Name = "session_id")] string sessionId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return NotFound();

        var info = await membershipService.GetConfirmationBySessionAsync(sessionId, ct);
        if (info is null) return NotFound();

        // Shown here as well as emailed: the member is looking at the screen right now, and making
        // them go and find an email to continue is a needless place to lose them. The link is a
        // standard single-use Identity reset token, so showing it costs nothing extra — reaching
        // this page already required the unguessable Stripe session id.
        string? setupUrl = null;
        if (info.IsConfirmed && info.UserId is { } userId
            && await userManager.FindByIdAsync(userId.ToString()) is { } user
            && !await userManager.HasPasswordAsync(user))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            setupUrl = Url.Page("/Account/ResetPassword", pageHandler: null,
                values: new { area = "Identity", code = encoded }, protocol: Request.Scheme);
        }

        ViewData["Title"] = info.IsConfirmed ? "Welcome to The VI House" : "Confirming your payment";
        return View(new JoinSuccessViewModel(info, setupUrl));
    }
}
