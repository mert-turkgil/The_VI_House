using Microsoft.AspNetCore.Mvc;

namespace VIHouse.WebUI.Helpers;

public static class ReturnUrls
{
    /// <summary>
    /// A local returnUrl, or the home page. Never the sign-in page itself: that would nest the whole
    /// URL as its own returnUrl on every round trip (through Google, say), until the "auth" rate
    /// limiter answers 429 — a real incident.
    /// </summary>
    public static string Safe(IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl)
        && url.IsLocalUrl(returnUrl)
        && !returnUrl.StartsWith(VIHouse.Business.SiteUrls.Login, StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : url.Content("~/");
}
