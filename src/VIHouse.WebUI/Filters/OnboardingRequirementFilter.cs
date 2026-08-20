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
/// invites and someone's payment history is worth protecting properly.
///
/// The gate reads each endpoint's own <see cref="AuthorizeAttribute"/> metadata rather than a
/// hand-maintained list of controllers, so anything written later is covered the moment it requires
/// a login. Anonymous endpoints are untouched, which is what keeps the public site and — critically
/// — the apply/join/checkout path reachable: a new member fills in the form and pays *before* they
/// have an account to secure.
/// </summary>
public class OnboardingRequirementFilter(UserManager<ApplicationUser> userManager) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
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

        // The Onboarding controller and the Identity area host the very pages this filter sends
        // people to — gating them would trap the account in a redirect loop with no way out.
        var area = context.RouteData.Values["area"] as string;
        var controller = context.RouteData.Values["controller"] as string;
        var isEscapeHatch =
            string.Equals(area, "Identity", StringComparison.OrdinalIgnoreCase)
            || string.Equals(controller, "Onboarding", StringComparison.OrdinalIgnoreCase);

        if (isEscapeHatch)
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
}
