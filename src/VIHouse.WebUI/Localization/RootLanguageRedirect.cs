using Microsoft.AspNetCore.Localization;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// Sends a returning visitor from the bare site root to the language they last chose.
///
/// This is the one place the culture cookie is still read, and it exists so that taking the cookie
/// out of culture resolution (see <see cref="RouteCultureProvider"/>) does not also throw away
/// "remember my language". Everywhere else the URL is believed exactly as written.
///
/// Only the root, and only an exact match on "/". Redirecting anything deeper would break the
/// guarantee the whole change was made for: that a link opens in the language it was written in.
/// A crawler sends no cookie, so it is never redirected and the root indexes as English, which is
/// what the canonical tag and the x-default both say it is.
/// </summary>
public class RootLanguageRedirect(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldRedirect(context, out var code))
        {
            // 302 rather than 301: this depends on a cookie, so it is per-visitor and must never be
            // cached by a proxy or baked into a browser's permanent redirect list.
            context.Response.Redirect($"/{code}{context.Request.QueryString}", permanent: false);
            return;
        }

        await next(context);
    }

    private static bool ShouldRedirect(HttpContext context, out string? code)
    {
        code = null;

        if (context.Request.Path != "/") return false;
        if (!HttpMethods.IsGet(context.Request.Method)) return false;

        var cookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
        if (string.IsNullOrWhiteSpace(cookie)) return false;

        var parsed = CookieRequestCultureProvider.ParseCookieValue(cookie);
        var culture = parsed?.UICultures.FirstOrDefault().Value;
        if (culture is null) return false;

        // Null for English, which lives at the root already — nothing to redirect to.
        code = SiteCultures.ToUrlCode(culture);
        return code is not null;
    }
}
