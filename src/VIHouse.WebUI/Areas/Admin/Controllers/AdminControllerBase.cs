using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Identity;

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
}
