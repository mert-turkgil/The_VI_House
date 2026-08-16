using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VIHouse.DataAccess.Identity;

namespace VIHouse.WebUI.Areas.Admin.Filters;

/// <summary>
/// Two-factor authentication is strongly recommended for admin accounts by the brief (§95) — this
/// enforces it rather than leaving it optional: any authenticated request into the Admin area from
/// a user who hasn't enabled 2FA yet is redirected to the setup page before it reaches the
/// controller. Registered globally (see Program.cs) since it only acts within the Admin area, so
/// there's no need to decorate every admin controller individually.
/// </summary>
public class AdminTwoFactorRequirementFilter(UserManager<ApplicationUser> userManager) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isAdminArea = string.Equals(context.RouteData.Values["area"] as string, "Admin", StringComparison.OrdinalIgnoreCase);
        var user = context.HttpContext.User;

        if (isAdminArea && user.Identity?.IsAuthenticated == true)
        {
            var appUser = await userManager.GetUserAsync(user);
            if (appUser is not null && !await userManager.GetTwoFactorEnabledAsync(appUser))
            {
                context.Result = new RedirectToPageResult(
                    "/Account/Manage/TwoFactorAuthentication",
                    new { area = "Identity" });
                return;
            }
        }

        await next();
    }
}
