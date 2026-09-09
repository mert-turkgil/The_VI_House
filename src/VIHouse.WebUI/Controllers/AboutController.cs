using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Controllers;

[Route("about")]
public class AboutController(IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = loc["About.Title"];
        this.SetSeo(loc["Seo.About.Description"].Value, canonicalPath: "/about");
        return View();
    }
}
