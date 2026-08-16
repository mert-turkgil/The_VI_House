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
[Authorize(Roles = RolesCsv)]
public abstract class AdminControllerBase : Controller
{
    protected const string RolesCsv =
        $"{Roles.SuperAdmin},{Roles.EventManager},{Roles.Finance},{Roles.Marketing},{Roles.Concierge},{Roles.Support}";
}
