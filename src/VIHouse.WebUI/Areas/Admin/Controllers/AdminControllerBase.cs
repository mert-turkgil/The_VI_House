using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Base for every controller in the Admin area. Authorization here is deliberately broad (any
/// admin-side role) — individual controllers/actions narrow further with their own
/// [Authorize(Roles = ...)] where a role split matters (e.g. only Finance touches refunds), per
/// the role table in brief §96.
/// </summary>
[Area("Admin")]
[Authorize(Roles = AdminSections.RolesFor.Everyone)]
public abstract class AdminControllerBase : Controller
{
    /// <summary>Every admin-side role — the outer gate. Each controller narrows to its section
    /// with <see cref="AdminSections.RolesFor"/>, which is also what draws the sidebar.</summary>
    protected const string RolesCsv = AdminSections.RolesFor.Everyone;

    /// <summary>The signed-in admin, for audit entries.</summary>
    protected Guid CurrentAdminId() => User.RequiredUserId();

    /// <summary>The caller's address, for audit entries.</summary>
    protected string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();

    protected (Guid AdminId, string? IpAddress) CurrentActor() => (CurrentAdminId(), Ip());

    /// <summary>
    /// The one-line message the next page shows after a redirect. A refusal is flagged so the panel
    /// presents it as an error that waits to be dismissed — telling the two apart from the wording
    /// only ever worked in English.
    /// </summary>
    protected void Status(string? message, bool isError = false)
    {
        TempData["StatusMessage"] = message;
        if (isError) TempData["StatusIsError"] = true;
    }
}
