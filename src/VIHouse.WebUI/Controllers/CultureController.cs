using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Language switcher target: sets the culture cookie and bounces back to where the visitor was.
/// A plain GET (not a POST/form) so the nav's language links can stay hardcoded &lt;a href&gt; tags
/// like every other pre-existing nav link — this isn't a state mutation in the CSRF-relevant sense.
///
/// The accept-list comes from SiteCultures rather than a local copy: seminar content is stored per
/// culture, so a language this controller accepts but the content layer does not know about would
/// switch the chrome and silently fall back to English for everything that matters.
/// </summary>
[Route("culture")]
public class CultureController : Controller
{
    [HttpGet("set")]
    public IActionResult Set(string culture, string? returnUrl)
    {
        if (SiteCultures.IsSupported(culture))
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });
        }

        return LocalRedirect(!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
