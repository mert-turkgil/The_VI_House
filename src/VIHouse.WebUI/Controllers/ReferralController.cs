using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.WebUI.Services;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Brief §47: "thevihouse.com/r/anton" — records a visit (with UTM attribution, brief §48) and sets
/// a 30-day cookie, then bounces to home. A bad/unknown code fails open (still redirects, just
/// doesn't set a cookie) rather than showing an error to what might be a real prospective member.
/// </summary>
[Route("r")]
public class ReferralController(IAmbassadorService ambassadorService) : Controller
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Visit(
        string code, [FromQuery] string? utm_source, [FromQuery] string? utm_medium,
        [FromQuery] string? utm_campaign, [FromQuery] string? utm_content, CancellationToken ct)
    {
        var ambassador = await ambassadorService.GetByCodeAsync(code, ct);
        if (ambassador is not null)
        {
            await ambassadorService.RecordVisitAsync(code, utm_source, utm_medium, utm_campaign, utm_content, ct);

            Response.Cookies.Append(ReferralCookie.Name, code, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                IsEssential = true,
            });
        }

        return RedirectToAction("Index", "Home");
    }
}
