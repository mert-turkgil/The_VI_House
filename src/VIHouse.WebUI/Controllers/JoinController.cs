using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Users;
using VIHouse.WebUI.Helpers;
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
/// step, so nothing here creates an account at all — the form is held as a PendingJoin, and the
/// account comes into being in the webhook once Stripe confirms payment. Until then the person can
/// resubmit, change plan, or come back through the resume link, and none of it collides.
///
/// Gated by Features:MembershipSales only where a charge can start (the form and the resume POST).
/// The welcome and resume pages read local state and stay reachable with the flag off, so switching
/// sales off never 404s someone who has already paid.
/// </summary>
[Route("join")]
public class JoinController(
    IMembershipService membershipService,
    IExperienceService experienceService,
    UserManager<ApplicationUser> userManager,
    IOptions<FeatureOptions> features) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid? plan, CancellationToken ct)
    {
        if (!features.Value.MembershipSales) return NotFound();

        // Already signed in? The logged-in purchase path already exists and knows who they are.
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Membership");

        var form = new JoinFormViewModel
        {
            Plans = await LoadPlanCardsAsync(ct),
            PlanId = plan ?? Guid.Empty,
            OpenExperiences = await LoadOpenExperiencesAsync(ct),
        };
        if (form.FullPlanIds.Contains(form.PlanId)) form.PlanId = Guid.Empty;

        if (Request.Cookies.TryGetValue(ReferralCookie.Name, out var referral))
            form.ReferralCode = referral;

        ViewData["Title"] = "Join The VI House";
        return View(form);
    }

    /// <summary>The plan cards with their seat counts. Availability is only looked up for plans
    /// that have a limit — an unlimited plan has nothing to count.</summary>
    private async Task<List<MembershipPlanCardViewModel>> LoadPlanCardsAsync(CancellationToken ct)
    {
        var cards = new List<MembershipPlanCardViewModel>();
        foreach (var plan in await membershipService.GetActivePlansAsync(ct))
        {
            var availability = plan.MaxMembers is null ? null : await membershipService.GetPlanAvailabilityAsync(plan.Id, ct);
            cards.Add(MembershipPlanCardViewModel.FromEntity(plan, availability));
        }
        return cards;
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

            var culture = CultureInfo.CurrentUICulture.Name;
        return open.Select(e => ExperienceCardViewModel.FromEntity(e, culture)).ToList();
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> Index(JoinFormViewModel form, CancellationToken ct)
    {
        if (!features.Value.MembershipSales) return NotFound();

        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Membership");

        if (!form.AgreeToTerms)
            ModelState.AddModelError(nameof(form.AgreeToTerms), "You must agree to the Terms & Conditions and Privacy Policy to join.");

        if (form.PlanId == Guid.Empty)
            ModelState.AddModelError(nameof(form.PlanId), "Choose a plan to continue.");

        // Recovers a browser-autofilled country name ("Cyprus") back to its code ("CY") — see
        // Countries.Normalize and ApplicationController's identical fix for why ModelState.Remove
        // matters here too.
        form.Country = Countries.Normalize(form.Country);
        ModelState.Remove(nameof(form.Country));
        if (!Countries.IsValid(form.Country))
            ModelState.AddModelError(nameof(form.Country), "Choose your country.");

        if (form.EarningsBand is not null && !EarningsBand.IsValid(form.EarningsBand))
            ModelState.AddModelError(nameof(form.EarningsBand), "Choose a range from the list.");

        if (!ModelState.IsValid)
        {
            form.Plans = await LoadPlanCardsAsync(ct);
            form.OpenExperiences = await LoadOpenExperiencesAsync(ct);
            ViewData["Title"] = "Join The VI House";
            return View(form);
        }

        var result = await membershipService.InitiateJoinCheckoutAsync(
            new JoinRequest(
                form.PlanId,
                form.FirstName.Trim(),
                form.LastName.Trim(),
                form.Email.Trim(),
                form.Country.Trim().ToUpperInvariant(),
                form.City?.Trim(),
                form.ReferralCode)
            {
                JobTitle = form.JobTitle?.Trim(),
                AddressLine1 = form.AddressLine1?.Trim(),
                AddressLine2 = form.AddressLine2?.Trim(),
                PostalCode = form.PostalCode?.Trim(),
                About = form.About.Trim(),
                Expectations = form.Expectations.Trim(),
                EarningsBand = form.EarningsBand,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                PromoCode = form.PromoCode,
            },
            SuccessUrl(), CancelUrlTemplate(), ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            form.Plans = await LoadPlanCardsAsync(ct);
            form.OpenExperiences = await LoadOpenExperiencesAsync(ct);
            ViewData["Title"] = "Join The VI House";
            return View(form);
        }

        return Redirect(result.CheckoutUrl!);
    }

    /// <summary>
    /// The page behind the link in the "your checkout expired" email, and where Stripe's own
    /// "back" button lands. Read-only: it shows what the person was buying and offers to reopen
    /// checkout. Deliberately not the thing that opens the checkout — a GET that creates a provider
    /// session does so for every link-preview and mail-scanner fetch too (see AccountController.Billing).
    /// </summary>
    [HttpGet("resume/{code}")]
    public async Task<IActionResult> Resume(string code, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Membership");

        var info = await membershipService.GetPendingJoinByCodeAsync(code, ct);
        if (info is null) return NotFound();

        // Already paid — the welcome page is the right place, not another checkout.
        if (info.IsPaid && info.PaidSessionId is not null)
            return RedirectToAction(nameof(Success), new { session_id = info.PaidSessionId });

        ViewData["Title"] = "Pick up where you left off";
        return View(new JoinResumeViewModel(info, features.Value.MembershipSales));
    }

    [HttpPost("resume/{code}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("checkout")]
    public async Task<IActionResult> ResumePost(string code, CancellationToken ct)
    {
        if (!features.Value.MembershipSales) return NotFound();

        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Membership");

        var result = await membershipService.ResumeJoinCheckoutAsync(code, SuccessUrl(), CancelUrlTemplate(), ct);
        if (result.Success)
            return Redirect(result.CheckoutUrl!);

        var info = await membershipService.GetPendingJoinByCodeAsync(code, ct);
        if (info is null) return NotFound();

        ModelState.AddModelError(string.Empty, result.Error!);
        ViewData["Title"] = "Pick up where you left off";
        return View(nameof(Resume), new JoinResumeViewModel(info, features.Value.MembershipSales));
    }

    private string SuccessUrl()
    {
        var url = Url.Action(nameof(Success), "Join", null, Request.Scheme)!;
        return url + (url.Contains('?') ? "&" : "?") + "session_id={CHECKOUT_SESSION_ID}";
    }

    /// <summary>The cancel URL points at the resume page for the row being paid — so "back" from
    /// Stripe lands somewhere that can restart, not on a blank form. The code is only known once
    /// the row exists, so this is a template the service fills in.</summary>
    private string CancelUrlTemplate() =>
        Url.Action(nameof(Resume), "Join", new { code = "__code__" }, Request.Scheme)!.Replace("__code__", "{code}");

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
