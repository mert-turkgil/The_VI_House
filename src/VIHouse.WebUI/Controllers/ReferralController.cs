using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using QRCoder;
using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.Entities.Referrals;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.Services;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// The ambassador's links (brief §47: "thevihouse.com/r/anton"), one per thing they promote:
///
///   /r/{code}            → the homepage
///   /r/{code}/e/{slug}   → one experience's page
///   /r/{code}/s/{slug}   → one session's page
///   /r/{code}/qr.svg|png → the QR code for any of the three (?to=e:{slug} / s:{slug}), for a
///                          story, a video description or a printed card
///
/// Each landing records a visit (with UTM attribution, brief §48, and the target) and sets the
/// 30-day attribution cookies, then bounces to the page. A bad or unknown code fails open — it
/// still redirects, just without a cookie — rather than showing an error to what might be a real
/// prospective member; an unknown slug lands on the listing instead of a 404 for the same reason.
/// </summary>
[Route("r")]
public class ReferralController(
    IAmbassadorService ambassadorService,
    IExperienceService experienceService,
    ISeminarService seminarService,
    ReferralFingerprint fingerprint,
    IOptions<SiteOptions> siteOptions) : Controller
{
    [HttpGet("{code}")]
    public async Task<IActionResult> Visit(string code, CancellationToken ct)
    {
        await RecordAsync(code, ReferralTargetKind.Site, null, SiteUrls.Home, ct);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("{code}/e/{slug}")]
    public async Task<IActionResult> VisitExperience(string code, string slug, CancellationToken ct)
    {
        var experience = await experienceService.GetPublicDetailBySlugAsync(slug, ct);
        if (experience is null)
        {
            await RecordAsync(code, ReferralTargetKind.Site, null, SiteUrls.Experiences, ct);
            return RedirectToAction("Index", "Experiences");
        }

        await RecordAsync(code, ReferralTargetKind.Experience, experience.Id, SiteUrls.Experience(experience.Slug), ct);
        return RedirectToAction("Details", "Experiences", new { slug = experience.Slug });
    }

    [HttpGet("{code}/s/{slug}")]
    public async Task<IActionResult> VisitSession(string code, string slug, CancellationToken ct)
    {
        // Members-only sessions are visible to the link: the page itself explains how to join.
        var seminar = await seminarService.GetPublicDetailBySlugAsync(slug, viewerIsMember: true, ct: ct);
        if (seminar is null)
        {
            await RecordAsync(code, ReferralTargetKind.Site, null, SiteUrls.Sessions, ct);
            return RedirectToAction("Index", "Seminars");
        }

        await RecordAsync(code, ReferralTargetKind.Session, seminar.Id, SiteUrls.Session(seminar.Slug), ct);
        return RedirectToAction("Details", "Seminars", new { slug = seminar.Slug });
    }

    /// <summary>The QR as an SVG — crisp at any size, right for a slide or a printed card.</summary>
    [HttpGet("{code}/qr.svg")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> QrSvgImage(string code, string? to, CancellationToken ct)
    {
        var url = await LinkForAsync(code, to, ct);
        if (url is null) return NotFound();
        return Content(QrSvg.Render(url), "image/svg+xml");
    }

    /// <summary>The same QR as a 512px PNG, for platforms that will not take an SVG (Instagram,
    /// most messaging apps).</summary>
    [HttpGet("{code}/qr.png")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> QrPngImage(string code, string? to, CancellationToken ct)
    {
        var url = await LinkForAsync(code, to, ct);
        if (url is null) return NotFound();

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(
            pixelsPerModule: 16,
            darkColorRgba: [0x00, 0x23, 0x0a, 0xff],
            lightColorRgba: [0xff, 0xff, 0xff, 0xff]);
        return File(png, "image/png", $"vi-house-{code}{(to is null ? "" : "-" + to.Replace(':', '-'))}.png");
    }

    /// <summary>
    /// The absolute link a QR should encode, or null when the code is not an active ambassador's —
    /// the image is public, so an unknown code must not get a QR minted for it. "to" is
    /// "e:{slug}" or "s:{slug}"; anything else means the plain site link.
    /// </summary>
    private async Task<string?> LinkForAsync(string code, string? to, CancellationToken ct)
    {
        var ambassador = await ambassadorService.GetByCodeAsync(code, ct);
        if (ambassador is null || ambassador.Status != AmbassadorStatus.Active) return null;

        var path = SiteUrls.Referral(ambassador.Code);
        if (to is { Length: > 2 } && to[1] == ':')
        {
            var slug = to[2..];
            path = to[0] switch
            {
                'e' when await experienceService.GetPublicDetailBySlugAsync(slug, ct) is not null => SiteUrls.ReferralExperience(ambassador.Code, slug),
                's' when await seminarService.GetPublicDetailBySlugAsync(slug, viewerIsMember: true, ct: ct) is not null => SiteUrls.ReferralSession(ambassador.Code, slug),
                _ => path,
            };
        }

        return SiteUrls.Absolute(BaseUrl, path);
    }

    private string BaseUrl =>
        string.IsNullOrWhiteSpace(siteOptions.Value.BaseUrl) ? $"{Request.Scheme}://{Request.Host}" : siteOptions.Value.BaseUrl;

    private async Task RecordAsync(string code, ReferralTargetKind kind, Guid? targetId, string landingPath, CancellationToken ct)
    {
        var ambassador = await ambassadorService.GetByCodeAsync(code, ct);
        // Only a live link attributes: a pending invitation or a paused ambassador must not
        // leave a 30-day cookie that would credit them later.
        if (ambassador is null || ambassador.Status != AmbassadorStatus.Active) return;

        var q = Request.Query;
        await ambassadorService.RecordVisitAsync(code, kind, targetId, landingPath,
            q["utm_source"], q["utm_medium"], q["utm_campaign"], q["utm_content"], fingerprint.For(HttpContext), ct);

        ReferralCookie.Write(Response, ambassador.Code);
    }
}
