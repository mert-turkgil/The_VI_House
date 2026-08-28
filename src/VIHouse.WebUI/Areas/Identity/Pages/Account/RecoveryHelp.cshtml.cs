using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VIHouse.WebUI.Areas.Identity.Pages.Account;

/// <summary>
/// What to do when both the authenticator app and the recovery codes are gone.
///
/// This page had no equivalent anywhere on the site. Two-step verification is mandatory here
/// (OnboardingRequirementFilter), so an account in that state is completely locked out, and the only
/// thing the product said about it was a warning — on a page you can no longer reach — that this
/// would happen.
///
/// The mechanism to fix it already existed: AdminUsersController.ResetTwoFactor, SuperAdmin-only and
/// audit-logged. Nothing told a member it existed or how to ask for it. This is that missing half.
///
/// Static content, so there is no model state and nothing to post. AllowAnonymous because the person
/// reading it cannot, by definition, complete a sign-in — and it is listed in
/// OnboardingRequirementFilter's escape hatch so a half-onboarded account can reach it too, which is
/// exactly the population most likely to be stuck here.
/// </summary>
[AllowAnonymous]
public class RecoveryHelpModel : PageModel
{
    public void OnGet()
    {
    }
}
