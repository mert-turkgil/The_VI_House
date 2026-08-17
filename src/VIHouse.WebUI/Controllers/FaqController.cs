using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Controllers;

[Route("faq")]
public class FaqController(IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = loc["Faq.Title"];
        return View();
    }
}
