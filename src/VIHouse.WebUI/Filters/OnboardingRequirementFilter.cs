using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VIHouse.DataAccess.Identity;

namespace VIHouse.WebUI.Filters;

/// <summary>
/// Holds a signed-in account at the onboarding checklist until it is actually safe to use: the
/// email address has been proven, and two-factor authentication is switched on. Both are mandatory
/// for members and admins alike — an account that can see the member directory, the community
/// invites and someone's payment history is worth protecting properly. For the two bootstrap admin
/// accounts on a fresh deployment this is the *only* thing standing between a leaked seed password
/// and the whole panel, so it has to hold on the very first sign-in with no manual setup step.
///
/// The gate reads each endpoint's own <see cref="AuthorizeAttribute"/> metadata rather than a
/// hand-maintained list of controllers, so anything written later is covered the moment it requires
/// a login. Anonymous endpoints are untouched, which is what keeps the public site and — critically
/// — the apply/join/checkout path reachable: a new member fills in the form and pays *before* they
/// have an account to secure.
///
/// Implemented as a <em>resource</em> filter rather than an action filter on purpose. Global
/// <see cref="IAsyncActionFilter"/>s never run for Razor Pages (page handlers use the separate
/// IPageFilter pipeline), which used to leave the whole Identity area — including
/// Manage/Disable2fa, Manage/Email and Manage/DownloadPersonalData — reachable by an account that
/// had not switched two-factor on yet. Resource filters run for both pipelines, so one registration
/// now covers MVC controllers and Identity's pages alike.
/// </summary>
public class OnboardingRequirementFilter(UserManager<ApplicationUser> userManager) : IAsyncResourceFilter
{
    /// <summary>
    /// The Identity pages that must stay reachable while an account is still incomplete. Everything
    /// else under /Identity is gated like any other authorized endpoint.
    ///
    /// Deliberately an allow-list, not a blanket "the Identity area is exempt": the escape hatch
    /// only needs to cover signing in, signing out, and recovering a password. Account *management*
    /// is not part of finishing setup, and letting it through meant a half-secured session could
    /// change the account's email address or export its personal data before 2FA ever went on.
    ///
    /// Most of these are [AllowAnonymous] anyway and would pass the check below regardless; they
    /// are listed so that the set is stated in one place rather than inferred from attributes
    /// scattered across a dozen scaffolded page models.
    /// </summary>
    private static readonly HashSet<string> IdentityEscapeHatchPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Login",
        "/Account/Logout",
        "/Account/LoginWith2fa",
        "/Account/LoginWithRecoveryCode",
        "/Account/Lockout",
        "/Account/AccessDenied",
        "/Account/ForgotPassword",
        "/Account/ForgotPasswordConfirmation",
        "/Account/ResetPassword",
        "/Account/ResetPasswordConfirmation",
        "/Account/ConfirmEmail",
        "/Account/ConfirmEmailChange",
        "/Account/ResendEmailConfirmation",
        // Whoever has lost both their authenticator and their recovery codes is, by definition,
        // stuck at the gate — so the page explaining what to do next has to sit outside it.
        "/Account/RecoveryHelp",
        "/Account/ExternalLogin",
        "/Error",
    };

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var metadata = context.ActionDescriptor.EndpointMetadata;

        // [AllowAnonymous] beats an [Authorize] inherited from the controller, the same precedence
        // the authorization middleware itself applies.
        var requiresAuth = metadata.OfType<IAuthorizeData>().Any()
            && !metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuth || user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        if (IsEscapeHatch(context))
        {
            await next();
            return;
        }

        var appUser = await userManager.GetUserAsync(user);
        if (appUser is not null)
        {
            var emailConfirmed = await userManager.IsEmailConfirmedAsync(appUser);
            var twoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(appUser);

            if (!emailConfirmed || !twoFactorEnabled)
            {
                context.Result = new RedirectToActionResult("Index", "Onboarding", new { area = "" });
                return;
            }
        }

        await next();
    }

    /// <summary>
    /// The Onboarding controller hosts the very pages this filter sends people to, and the Identity
    /// allow-list above covers signing in/out — gating either would trap the account in a redirect
    /// loop with no way out.
    /// </summary>
    private static bool IsEscapeHatch(ResourceExecutingContext context)
    {
        var controller = context.RouteData.Values["controller"] as string;
        if (string.Equals(controller, "Onboarding", StringComparison.OrdinalIgnoreCase))
            return true;

        var area = context.RouteData.Values["area"] as string;
        if (!string.Equals(area, "Identity", StringComparison.OrdinalIgnoreCase))
            return false;

        // Razor Pages put the page path (e.g. "/Account/Manage/Disable2fa") here; an MVC controller
        // in an "Identity" area would not, and nothing in that position should be exempt anyway.
        return context.RouteData.Values["page"] is string page && IdentityEscapeHatchPages.Contains(page);
    }
}
