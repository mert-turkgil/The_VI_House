using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.WebUI.ViewModels.Content;

namespace VIHouse.WebUI.Controllers;

[Route("legal")]
public class LegalController(IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("terms")]
    public IActionResult Terms() => ShowDocument("Footer.Terms", "Legal.Terms.Body");

    [HttpGet("privacy")]
    public IActionResult Privacy() => ShowDocument("Footer.Privacy", "Legal.Privacy.Body");

    [HttpGet("cookies")]
    public IActionResult Cookies() => ShowDocument("Footer.Cookies", "Legal.Cookies.Body");

    [HttpGet("refund")]
    public IActionResult Refund() => ShowDocument("Footer.RefundPolicy", "Legal.Refund.Body");

    private IActionResult ShowDocument(string titleKey, string bodyKey)
    {
        ViewData["Title"] = loc[titleKey];
        return View("Show", new LegalDocumentViewModel { TitleKey = titleKey, BodyKey = bodyKey });
    }
}
