using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
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
/// [Authorize] throughout, but deliberately exempt from OnboardingRequirementFilter (which routes
/// people *here*), so these pages stay reachable while the account is still incomplete.
/// </summary>
[Authorize]
[Route("onboarding")]
public class OnboardingController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailService emailService,
    IMembershipService membershipService,
    UrlEncoder urlEncoder) : Controller
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var model = await BuildStatusAsync(user, ct);
        if (model.IsComplete) return RedirectToAction(nameof(Done));

        ViewData["Title"] = "Set up your account";
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

        TempData["StatusMessage"] = $"Confirmation link sent to {user.Email}. It's valid for 24 hours.";
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
            return View("ConfirmEmailResult", new ConfirmEmailResultViewModel(false, "That confirmation link is incomplete."));

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return View("ConfirmEmailResult", new ConfirmEmailResultViewModel(false, "We couldn't find that account."));

        string token;
        try
        {
            token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return View("ConfirmEmailResult", new ConfirmEmailResultViewModel(false, "That confirmation link is malformed."));
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        ViewData["Title"] = result.Succeeded ? "Email confirmed" : "Confirmation failed";

        return View("ConfirmEmailResult", result.Succeeded
            ? new ConfirmEmailResultViewModel(true, null)
            : new ConfirmEmailResultViewModel(false, "That link has expired or has already been used. Sign in and request a new one."));
    }

    // --- Step: two-factor authentication -------------------------------------------------------

    [HttpGet("two-factor")]
    public async Task<IActionResult> TwoFactor(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (await userManager.GetTwoFactorEnabledAsync(user))
            return RedirectToAction(nameof(Index));

        ViewData["Title"] = "Set up two-factor authentication";
        return View(await BuildAuthenticatorModelAsync(user));
    }

    [HttpPost("two-factor")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> TwoFactor(TwoFactorSetupViewModel form, CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var code = (form.VerificationCode ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

        var isValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        if (!isValid)
        {
            ModelState.AddModelError(nameof(form.VerificationCode),
                "That code didn't match. Codes change every 30 seconds — wait for a fresh one and try again. If it keeps failing, check your phone's clock is set automatically.");

            ViewData["Title"] = "Set up two-factor authentication";
            return View(await BuildAuthenticatorModelAsync(user));
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);

        // Regenerated here rather than shown from an earlier step: these are the only way back in if
        // the phone is lost, and they must be presented exactly once, at the point they become real.
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        // Refresh the cookie so the new 2FA state is reflected without forcing a re-login.
        await signInManager.RefreshSignInAsync(user);

        TempData["RecoveryCodes"] = string.Join(",", recoveryCodes ?? []);
        return RedirectToAction(nameof(RecoveryCodes));
    }

    [HttpGet("recovery-codes")]
    public IActionResult RecoveryCodes()
    {
        if (TempData["RecoveryCodes"] is not string joined || string.IsNullOrWhiteSpace(joined))
            return RedirectToAction(nameof(Index));

        ViewData["Title"] = "Save your recovery codes";
        return View(joined.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
    }

    // --- Finish ---------------------------------------------------------------------------------

    [HttpGet("done")]
    public async Task<IActionResult> Done(CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var status = await BuildStatusAsync(user, ct);
        if (!status.IsComplete) return RedirectToAction(nameof(Index));

        ViewData["Title"] = "You're all set";
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
