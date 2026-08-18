using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.ViewModels.Ambassador;

namespace VIHouse.WebUI.Controllers;

/// <summary>The ambassador-facing dashboard (brief §49) — a partner's own view of their referral
/// performance. English-only by deliberate scope call, same as Admin: this is a semi-operational
/// business-partner tool, not a general member-facing page. Shows aggregate stats only, never the
/// names/emails of who was referred (brief's explicit privacy rule).</summary>
[Authorize(Roles = Roles.Ambassador)]
[Route("ambassador")]
public class AmbassadorController(IAmbassadorService ambassadorService, UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = Guid.Parse(userManager.GetUserId(User)!);
        var ambassador = await ambassadorService.GetByUserIdAsync(userId, ct);
        if (ambassador is null) return NotFound();

        var stats = await ambassadorService.GetStatsAsync(ambassador.Id, ct);

        ViewData["Title"] = "Ambassador Dashboard";
        return View(new AmbassadorDashboardViewModel
        {
            Name = ambassador.Name,
            Code = ambassador.Code,
            CommissionPercent = ambassador.CommissionPercent,
            Stats = stats,
        });
    }
}
