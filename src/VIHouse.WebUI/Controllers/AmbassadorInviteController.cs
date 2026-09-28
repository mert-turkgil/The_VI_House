using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.ViewModels.Ambassador;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Where an invited ambassador lands from their email (/ambassador/invite/{token}). There is no
/// public way to become an ambassador: without a live token this page says the link is not valid
/// and nothing else. Accepting creates (or attaches) the account, confirms the email — only the
/// person holding the email could have followed the link — records the terms consent and payout
/// details, and switches the links on. See AmbassadorService.AcceptInviteAsync.
/// </summary>
[AllowAnonymous]
[Route("ambassador/invite")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AmbassadorInviteController(
    IAmbassadorService ambassadorService,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("{token}")]
    public async Task<IActionResult> Index(string token, CancellationToken ct)
    {
        Prepare();
        var lookup = await ambassadorService.GetInviteAsync(token, ct);
        if (lookup.State != AmbassadorInviteState.Valid) return Unavailable(lookup.State);

        if (Gate(lookup) is { } gate) return gate;

        var ambassador = lookup.Ambassador!;
        return View(Fill(new AmbassadorInviteViewModel
        {
            FirstName = lookup.AccountFirstName ?? "",
            LastName = lookup.AccountLastName ?? "",
            Country = lookup.AccountCountry ?? "",
            AccountHolder = "",
        }, token, lookup));
    }

    [HttpPost("{token}")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Index(string token, AmbassadorInviteViewModel form, CancellationToken ct)
    {
        Prepare();
        var lookup = await ambassadorService.GetInviteAsync(token, ct);
        if (lookup.State != AmbassadorInviteState.Valid) return Unavailable(lookup.State);
        if (Gate(lookup) is { } gate) return gate;

        Fill(form, token, lookup);

        if (form.NeedsPassword && string.IsNullOrEmpty(form.Password))
            ModelState.AddModelError(nameof(form.Password), loc["AmbassadorInvite.Error.Password"]);
        if (!string.IsNullOrWhiteSpace(form.Iban) && !Iban.IsValid(form.Iban))
            ModelState.AddModelError(nameof(form.Iban), loc["AmbassadorInvite.Error.IbanInvalid"]);
        if (!Iban.IsValidBic(form.Bic))
            ModelState.AddModelError(nameof(form.Bic), loc["AmbassadorInvite.Error.Bic"]);
        if (!string.IsNullOrWhiteSpace(form.Country) && !Countries.IsValid(form.Country))
            ModelState.AddModelError(nameof(form.Country), loc["AmbassadorInvite.Error.Country"]);
        if (!form.AcceptTerms)
            ModelState.AddModelError(nameof(form.AcceptTerms), loc["AmbassadorInvite.Error.Terms"]);
        if (!ModelState.IsValid) return View(form);

        var result = await ambassadorService.AcceptInviteAsync(token, new AmbassadorAcceptance
        {
            FirstName = form.FirstName,
            LastName = form.LastName,
            Password = form.NeedsPassword ? form.Password : null,
            AddressLine1 = form.AddressLine1,
            AddressLine2 = form.AddressLine2,
            City = form.City,
            PostalCode = form.PostalCode,
            Country = form.Country,
            TaxId = form.TaxId,
            AccountHolder = form.AccountHolder,
            Iban = form.Iban,
            Bic = form.Bic,
            TermsText = string.Join("\n", form.Terms),
            AcceptedTerms = form.AcceptTerms,
        }, CurrentUserId, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

        switch (result.Status)
        {
            case AmbassadorAcceptStatus.Activated:
                var user = await userManager.FindByIdAsync(result.UserId!.Value.ToString());
                if (user is not null)
                {
                    // Signed straight in: the Ambassador role has to be in the cookie. In Production
                    // the onboarding gate then walks them through two-step verification first.
                    if (CurrentUserId == user.Id) await signInManager.RefreshSignInAsync(user);
                    else await signInManager.SignInAsync(user, isPersistent: false);
                }
                TempData["StatusMessage"] = loc["AmbassadorInvite.Welcome"].Value;
                return LocalRedirect(SiteUrls.InCulture(SiteUrls.Ambassador, CultureInfo.CurrentUICulture.Name));

            case AmbassadorAcceptStatus.Invalid:
            case AmbassadorAcceptStatus.Expired:
                return Unavailable(result.Status == AmbassadorAcceptStatus.Expired ? AmbassadorInviteState.Expired : AmbassadorInviteState.Invalid);

            case AmbassadorAcceptStatus.SignInRequired:
            case AmbassadorAcceptStatus.WrongAccount:
                return Gate(lookup) ?? View(form);

            default:
                foreach (var error in result.Errors)
                {
                    var (field, key) = error switch
                    {
                        "Iban" => (nameof(form.Iban), "AmbassadorInvite.Error.IbanInvalid"),
                        "Bic" => (nameof(form.Bic), "AmbassadorInvite.Error.Bic"),
                        "Terms" => (nameof(form.AcceptTerms), "AmbassadorInvite.Error.Terms"),
                        "Name" => (nameof(form.FirstName), "AmbassadorInvite.Error.FirstName"),
                        "AccountHolder" => (nameof(form.AccountHolder), "AmbassadorInvite.Error.AccountHolder"),
                        "Password" => (nameof(form.Password), "AmbassadorInvite.Error.Password"),
                        // Identity's own password-rule messages.
                        _ => (nameof(form.Password), null),
                    };
                    ModelState.AddModelError(field, key is null ? error : loc[key].Value);
                }
                return View(form);
        }
    }

    // --- helpers ---------------------------------------------------------------------------------

    private Guid? CurrentUserId =>
        User.Identity?.IsAuthenticated == true && Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>The token is in the address: keep it out of referrers and search engines.</summary>
    private void Prepare()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        ViewData["Title"] = loc["AmbassadorInvite.Title"].Value;
    }

    /// <summary>
    /// An address that already has a login is opened by signing in with it, not by the link alone;
    /// and a browser signed in as somebody else is told so rather than attaching the wrong account.
    /// </summary>
    private IActionResult? Gate(AmbassadorInviteLookup lookup)
    {
        var current = CurrentUserId;
        if (lookup.AccountHasPassword && current != lookup.AccountId)
        {
            return View("Gate", new AmbassadorInviteGateViewModel(
                current is null ? AmbassadorInviteGate.SignIn : AmbassadorInviteGate.WrongAccount,
                lookup.Ambassador!.InviteEmail!, Request.Path + Request.QueryString));
        }
        if (!lookup.AccountHasPassword && current is { } signedIn && signedIn != lookup.AccountId)
        {
            return View("Gate", new AmbassadorInviteGateViewModel(AmbassadorInviteGate.WrongAccount,
                lookup.Ambassador!.InviteEmail!, Request.Path + Request.QueryString));
        }
        return null;
    }

    private IActionResult Unavailable(AmbassadorInviteState state)
    {
        Response.StatusCode = state == AmbassadorInviteState.Expired ? StatusCodes.Status410Gone : StatusCodes.Status404NotFound;
        return View("Unavailable", state);
    }

    private AmbassadorInviteViewModel Fill(AmbassadorInviteViewModel model, string token, AmbassadorInviteLookup lookup)
    {
        var a = lookup.Ambassador!;
        model.Token = token;
        model.AmbassadorName = a.Name;
        model.Code = a.Code;
        model.CommissionPercent = a.CommissionPercent;
        model.ExpiresAt = a.InviteExpiresAt!.Value;
        model.Email = a.InviteEmail!;
        model.NeedsPassword = !lookup.AccountHasPassword;
        var rate = a.CommissionPercent.ToString("0.##", CultureInfo.CurrentCulture);
        model.Terms =
        [
            loc["AmbassadorInvite.Terms.Commission", rate].Value,
            loc["AmbassadorInvite.Terms.Payouts"].Value,
            loc["AmbassadorInvite.Terms.Refunds"].Value,
            loc["AmbassadorInvite.Terms.OwnPurchases"].Value,
            loc["AmbassadorInvite.Terms.Changes"].Value,
            loc["AmbassadorInvite.Terms.Privacy"].Value,
        ];
        return model;
    }
}

public enum AmbassadorInviteGate { SignIn, WrongAccount }

public record AmbassadorInviteGateViewModel(AmbassadorInviteGate Gate, string Email, string ReturnUrl);
