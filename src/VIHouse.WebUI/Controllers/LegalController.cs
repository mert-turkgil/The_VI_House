using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.ViewModels.Content;

namespace VIHouse.WebUI.Controllers;

[Route("legal")]
public class LegalController(IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("terms")]
    public IActionResult Terms() => ShowDocument("Footer.Terms", "Legal.Terms.Body", "terms");

    [HttpGet("privacy")]
    public IActionResult Privacy() => ShowDocument("Footer.Privacy", "Legal.Privacy.Body", "privacy");

    [HttpGet("cookies")]
    public IActionResult Cookies() => ShowDocument("Footer.Cookies", "Legal.Cookies.Body", "cookies");

    [HttpGet("refund")]
    public IActionResult Refund() => ShowDocument("Footer.RefundPolicy", "Legal.Refund.Body", "refund");

    private IActionResult ShowDocument(string titleKey, string bodyKey, string pageKey)
    {
        ViewData["Title"] = loc[titleKey].Value;
        this.SetSeo(canonicalPath: VIHouse.Business.SeoPages.All.First(p => p.Key == pageKey).Path, pageKey: pageKey);
        return View("Show", new LegalDocumentViewModel { TitleKey = titleKey, BodyKey = bodyKey });
    }
}
