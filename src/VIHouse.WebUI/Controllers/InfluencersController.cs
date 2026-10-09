using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// "Become an influencer" — the one public page about the programme. There is no sign-up form: the
/// House chooses its influencers, so the page explains what the programme is and asks people to
/// write in, to the address set in Admin › Site &amp; SEO (Influencer programme email), or the
/// site's contact address when that is empty. Behind the launch curtain like every public page.
/// </summary>
[Route("influencers")]
public class InfluencersController(ISiteSettingsService siteSettings, IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var settings = await siteSettings.GetCachedAsync(ct);
        ViewData["Title"] = loc["Influencers.Title"].Value;
        this.SetSeo(loc["Seo.Influencers.Description"].Value, canonicalPath: "/influencers", pageKey: "influencers");
        return View(model: string.IsNullOrWhiteSpace(settings.InfluencerEmail) ? settings.ContactEmail : settings.InfluencerEmail);
    }
}
