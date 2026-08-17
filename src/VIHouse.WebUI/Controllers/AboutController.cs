using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Controllers;

[Route("about")]
public class AboutController(IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = loc["About.Title"];
        return View();
    }
}
