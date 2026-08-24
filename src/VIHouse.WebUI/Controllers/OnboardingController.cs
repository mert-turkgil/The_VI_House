using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.WebUI.ViewModels.Onboarding;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Everything between "the payment went through" and "this is a usable account": set a password,
/// prove the email address, switch on two-factor authentication.
///
/// It exists as its own controller rather than leaning on the stock Identity Manage pages because
/// this is the one moment where the person has no idea what any of it means — they came here to buy
/// something, not to configure security. The pages spell out each step in order, say why it's
/// needed, and never present more than one thing to do at a time.
///
/// This is also the enrolment path for the bootstrap admin accounts on a fresh deployment. They are
/// seeded with a password and nothing else; OnboardingRequirementFilter bounces their first sign-in
/// here, and the panel stays shut until the authenticator is paired and the recovery codes have
/// been acknowledged. No manual step and no environment switch — the same code path runs in
/// Development, so the flow can be rehearsed exactly as it will happen in Production.
///
/// [Authorize] throughout, but deliberately exempt from OnboardingRequirementFilter (which routes
/// people *here*), so these pages stay reachable while the account is still incomplete.
///
/// Every user-facing string — status messages and validation errors included — resolves through
/// IStringLocalizer against the culture cookie, so someone who set the site to Türkçe before
/// signing in is not dropped into English at the one point where the instructions actually matter.
/// </summary>
[Authorize]
[Route("onboarding")]
public class OnboardingController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailService emailService,
    IMembershipService membershipService,
    IAuditLogRepository auditLogs,
    IStringLocalizer<SharedResource> loc,
    UrlEncoder urlEncoder) : Controller
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    /// <summary>Carries the freshly generated recovery codes from the verify POST to the page that
    /// displays them. Held in TempData rather than regenerated on the next request, because
    /// generating a second set silently invalidates the first.</summary>
    private const string RecoveryCodesKey = "RecoveryCodes";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var model = await BuildStatusAsync(user, ct);
        if (model.IsComplete) return RedirectToAction(nameof(Done));

        ViewData["Title"] = loc["Onboarding.Title"].Value;
        return View(model);
    }

    // --- Step: confirm email ------------------------------------------------------------------

    [HttpPost("send-confirmation")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SendConfirmation(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (await userManager.IsEmailConfirmedAsync(user))
            return RedirectToAction(nameof(Index));

        await SendConfirmationEmailAsync(user, ct);

        TempData["StatusMessage"] = loc["Onboarding.Confirm.Sent", user.Email!].Value;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The link target from the confirmation email. AllowAnonymous because the member may well open
    /// it in a different browser (phone mail app, say) where they aren't signed in — the token in
    /// the URL is what proves ownership, not the session.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(string userId, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            return ConfirmEmailFailed("Onboarding.Confirm.Error.Incomplete");

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return ConfirmEmailFailed("Onboarding.Confirm.Error.NotFound");

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return ConfirmEmailFailed("Onboarding.Confirm.Error.Malformed");
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
            return ConfirmEmailFailed("Onboarding.Confirm.Error.Expired");

        ViewData["Title"] = loc["Onboarding.Confirm.Success.Title"].Value;
        return View("ConfirmEmailResult", new ConfirmEmailResultViewModel(true, null));
    }

    private ViewResult ConfirmEmailFailed(string errorKey)
    {
        ViewData["Title"] = loc["Onboarding.Confirm.Fail.Title"].Value;
        return View("ConfirmEmailResult", new ConfirmEmailResultViewModel(false, loc[errorKey].Value));
    }

    // --- Step: two-factor authentication -------------------------------------------------------

    [HttpGet("two-factor")]
    public async Task<IActionResult> TwoFactor(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (await userManager.GetTwoFactorEnabledAsync(user))
            return RedirectToAction(nameof(Index));

        ViewData["Title"] = loc["Onboarding.TwoFactor.Title"].Value;
        return View(await BuildAuthenticatorModelAsync(user));
    }

    [HttpPost("two-factor")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> TwoFactor(TwoFactorSetupViewModel form, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        // Authenticator apps display the code in groups, and people paste exactly what they see.
        var code = (form.VerificationCode ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

        var isValid = code.Length > 0 && await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
        {
            ModelState.AddModelError(nameof(form.VerificationCode), loc["Onboarding.TwoFactor.Error.Invalid"].Value);

            ViewData["Title"] = loc["Onboarding.TwoFactor.Title"].Value;
            return View(await BuildAuthenticatorModelAsync(user));
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);

        // Regenerated here rather than shown from an earlier step: these are the only way back in if
        // the phone is lost, and they must be presented exactly once, at the point they become real.
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);

        // Refresh the cookie so the new 2FA state is reflected without forcing a re-login.
        await signInManager.RefreshSignInAsync(user);

        await RecordTwoFactorEnabledAsync(user, ct);

        TempData[RecoveryCodesKey] = string.Join(",", recoveryCodes ?? []);
        return RedirectToAction(nameof(RecoveryCodes));
    }

    private const int RecoveryCodeCount = 10;

    [HttpGet("recovery-codes")]
    public IActionResult RecoveryCodes()
    {
        var codes = ReadRecoveryCodes();
        if (codes.Count == 0) return RedirectToAction(nameof(Index));

        ViewData["Title"] = loc["Onboarding.Codes.Title"].Value;
        return View(new RecoveryCodesViewModel { Codes = codes });
    }

    /// <summary>
    /// Gated behind an explicit acknowledgement rather than a plain "continue" link. The codes are
    /// shown exactly once, and whoever clicks past them has locked themselves out of their own
    /// account for the day they lose their phone — the tick box is the only thing standing between
    /// "shown" and "actually read". For an admin account it is the difference between a lost phone
    /// and a lost panel.
    /// </summary>
    [HttpPost("recovery-codes")]
    [ValidateAntiForgeryToken]
    public IActionResult RecoveryCodes(RecoveryCodesViewModel form)
    {
        var codes = ReadRecoveryCodes();
        if (codes.Count == 0) return RedirectToAction(nameof(Index));

        if (!form.Acknowledged)
        {
            ModelState.AddModelError(nameof(form.Acknowledged), loc["Onboarding.Codes.Error.MustAcknowledge"].Value);

            ViewData["Title"] = loc["Onboarding.Codes.Title"].Value;
            return View(new RecoveryCodesViewModel { Codes = codes });
        }

        // Acknowledged — drop them so a back-button press cannot re-display a set that now exists
        // only as hashes against the account.
        TempData.Remove(RecoveryCodesKey);
        return RedirectToAction(nameof(Done));
    }

    /// <summary>Peeks rather than reads: the codes have to survive a failed acknowledgement POST,
    /// and TempData is consumed by a normal read.</summary>
    private List<string> ReadRecoveryCodes() =>
        TempData.Peek(RecoveryCodesKey) is string joined && !string.IsNullOrWhiteSpace(joined)
            ? joined.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            : [];

    // --- Finish ---------------------------------------------------------------------------------

    [HttpGet("done")]
    public async Task<IActionResult> Done(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var status = await BuildStatusAsync(user, ct);
        if (!status.IsComplete) return RedirectToAction(nameof(Index));

        ViewData["Title"] = loc["Onboarding.Done.Title"].Value;
        return View(status);
    }

    // --- Helpers --------------------------------------------------------------------------------

    private async Task<OnboardingStatusViewModel> BuildStatusAsync(ApplicationUser user, CancellationToken ct)
    {
        var membership = await membershipService.GetCurrentMembershipAsync(user.Id, ct);
        var plan = membership is null ? null : await membershipService.GetPlanAsync(membership.PlanId, ct);

        return new OnboardingStatusViewModel
        {
            FirstName = user.FirstName,
            Email = user.Email ?? "",
            HasPassword = await userManager.HasPasswordAsync(user),
            EmailConfirmed = await userManager.IsEmailConfirmedAsync(user),
            TwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user),
            PlanName = plan?.Name,
            HasMembership = membership is not null,
            IsStaff = (await userManager.GetRolesAsync(user)).Intersect(Roles.AdminRoles).Any(),
        };
    }

    private async Task<TwoFactorSetupViewModel> BuildAuthenticatorModelAsync(ApplicationUser user)
    {
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await userManager.GetEmailAsync(user) ?? user.UserName!;

        return new TwoFactorSetupViewModel
        {
            SharedKey = FormatKey(key!),
            AuthenticatorUri = string.Format(
                AuthenticatorUriFormat,
                urlEncoder.Encode("The VI House"),
                urlEncoder.Encode(email),
                key),
        };
    }

    /// <summary>
    /// Switching two-factor on is a security-relevant state change, so it belongs in the same audit
    /// trail as every admin mutation — it is the record of when a given account stopped being
    /// password-only, and the counterpart to the AdminUsers "reset two-factor" entry. Written for
    /// members too: the volume is one row per account, once.
    /// </summary>
    private async Task RecordTwoFactorEnabledAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);

        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = user.Id,
            Action = "TwoFactorEnabled",
            EntityType = nameof(ApplicationUser),
            EntityId = user.Id,
            DataBefore = null,
            DataAfter = JsonSerializer.Serialize(new
            {
                user.Email,
                Roles = roles,
                Method = "Authenticator",
                RecoveryCodesIssued = RecoveryCodeCount,
            }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        }, ct);

        await auditLogs.SaveChangesAsync(ct);
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var link = Url.Action(nameof(ConfirmEmail), "Onboarding",
            new { userId = user.Id, code = encoded }, Request.Scheme)!;

        await emailService.SendAsync(
            "ConfirmEmail", user.Email!, "Confirm your email — The VI House",
            new ConfirmEmailAddressEmailModel(user.FirstName, link),
            nameof(ApplicationUser), user.Id, ct);
    }

    /// <summary>Groups the secret into fours — it's meant to be typed by hand into an authenticator
    /// app when the QR code can't be scanned, and an unbroken 32-character string is hard to keep
    /// your place in.</summary>
    private static string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var position = 0;

        while (position + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(position, 4)).Append(' ');
            position += 4;
        }
        if (position < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(position));
        }

        return result.ToString().ToLowerInvariant();
    }
}
