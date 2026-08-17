using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Language switcher target: sets the culture cookie and bounces back to where the visitor was.
/// A plain GET (not a POST/form) so the nav's language links can stay hardcoded &lt;a href&gt; tags
/// like every other pre-existing nav link — this isn't a state mutation in the CSRF-relevant sense.
/// </summary>
[Route("culture")]
public class CultureController : Controller
{
    private static readonly HashSet<string> Supported = ["en-GB", "de-DE", "tr-TR", "et-EE"];

    [HttpGet("set")]
    public IActionResult Set(string culture, string? returnUrl)
    {
        if (Supported.Contains(culture))
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
        }

        return LocalRedirect(!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
