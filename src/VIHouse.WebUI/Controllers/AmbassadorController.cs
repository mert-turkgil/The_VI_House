using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.ViewModels.Ambassador;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Controllers;

/// <summary>The ambassador-facing dashboard (brief §49) — a partner's own view of their links and
/// referral performance. Shows aggregate stats only, never the names/emails of who was referred
/// (the brief's explicit privacy rule). The links themselves are drawn by the same partial the
/// admin page uses (Views/Shared/_ReferralLinks).</summary>
[Authorize(Roles = Roles.Ambassador)]
[Route("ambassador")]
public class AmbassadorController(
    IAmbassadorService ambassadorService,
    IOptions<SiteOptions> siteOptions) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = User.RequiredUserId();
        var ambassador = await ambassadorService.GetByUserIdAsync(userId, ct);
        if (ambassador is null) return NotFound();

        var stats = await ambassadorService.GetStatsAsync(ambassador.Id, ct);

        // Site:BaseUrl is the host visitors use; the request host is right locally and right enough
        // anywhere the setting is missing. The same rule the admin page and the emails apply, so the
        // link an ambassador copies here is byte-for-byte the one they were emailed.
        var baseUrl = string.IsNullOrWhiteSpace(siteOptions.Value.BaseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : siteOptions.Value.BaseUrl;

        ViewData["Title"] = "Ambassador Dashboard";
        return View(new AmbassadorDashboardViewModel
        {
            Name = ambassador.Name,
            Code = ambassador.Code,
            CommissionPercent = ambassador.CommissionPercent,
            Stats = stats,
            Conversions = await ambassadorService.GetConversionsAsync(ambassador.Id, 50, ct),
            Links = new ReferralLinksViewModel
            {
                Code = ambassador.Code,
                BaseUrl = baseUrl,
                Targets = await ambassadorService.GetLinkTargetsAsync(ct),
                Stats = stats.Targets,
                Style = "site",
            },
        });
    }
}
