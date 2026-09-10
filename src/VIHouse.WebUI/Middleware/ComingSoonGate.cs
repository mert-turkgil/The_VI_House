using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;

namespace VIHouse.WebUI.Middleware;

/// <summary>
/// Holds the whole public site behind one page while <c>Features:ComingSoon</c> is on. Admins see
/// the finished site; everyone else sees the curtain.
///
/// Middleware rather than a resource filter, which is the opposite of the choice
/// <see cref="Filters.OnboardingRequirementFilter"/> documents at length. UseRouting has already run
/// by the time this executes, so it has the same route values and the same endpoint metadata a
/// filter would — plus the three things a filter cannot reach:
///
///   - a request that matched no endpoint at all. MVC filters never run for a 404, so under a
///     filter every real page would redirect while a mistyped URL returned a bare 404 — which tells
///     anyone probing exactly which paths are real.
///   - the MapStaticAssets endpoints. This app has no UseStaticFiles; static files are served by an
///     endpoint downstream of here, and endpoints have no ActionDescriptor, so a filter would leave
///     the whole of wwwroot ungated. That turns out to be what we want (see IsOpen) — but it needs
///     to be a decision rather than an accident.
///   - the authorization stage. AutoValidateAntiforgeryTokenAttribute is registered globally and
///     runs before every resource filter, so a POST arriving with a stale token would be answered
///     with a 400 instead of the curtain.
///
/// This is a curtain, not a lock. It decides what a visitor is *shown*; what they are *allowed* to
/// do is still decided by the [Authorize] attributes underneath, every one of which is unchanged.
/// Anything under wwwroot stays downloadable to whoever holds a URL.
/// </summary>
public class ComingSoonGate(RequestDelegate next, IOptionsMonitor<FeatureOptions> features)
{
    private const string CurtainPath = "/coming-soon";

    /// <summary>
    /// Matched on the controller <em>name</em>, never on the URL. CulturePrefixConvention gives
    /// every one of these a {culture:sitelang} twin and both routes resolve to the same controller,
    /// so nothing here has to know about SeoResolver.StripCulture, and renaming a [Route] template
    /// cannot silently open or close a door.
    ///
    /// Every entry is a mechanism rather than a page anyone browses: crawler files, a
    /// server-to-server callback, and an image byte-stream. No content page is reachable behind the
    /// curtain — Legal is deliberately absent, so /legal/privacy redirects like everything else, and
    /// the data-use notice lives inline on the curtain beside the email field instead.
    /// </summary>
    private static readonly HashSet<string> OpenControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        // The destination itself. Gating it is an infinite redirect.
        "ComingSoon",
        // /robots.txt, /sitemap.xml, /llms.txt. A crawler that cannot read robots.txt is worse off
        // than one reading a curtained site, and the sitemap narrows itself while the flag is on.
        "Seo",
        // Stripe does not follow redirects: a 302 here is a failed delivery, retried and then
        // abandoned. Payments taken before the curtain went up still need their callbacks.
        "Webhooks",
        // The curtain's own og:image resolves to /media/site-og/{id}, so gating this breaks the
        // link preview of the one page anyone is meant to share.
        "Media",
    };

    /// <summary>
    /// The Identity pages that stay open, and only these. Same shape as
    /// OnboardingRequirementFilter.IdentityEscapeHatchPages and deliberately narrower: that list has
    /// to cover recovering an account, this one only has to cover getting *in*.
    ///
    /// LoginWith2fa is the entry that must never be removed. Between the password and the code the
    /// visitor holds only the Identity.TwoFactorUserId cookie — User.Identity.IsAuthenticated is
    /// false and the role check in IsOpen cannot see an admin yet. Leave it out and every admin is
    /// bounced to the curtain mid-sign-in, which means nobody can ever get in and there is no way to
    /// fix it without a deployment.
    /// </summary>
    private static readonly HashSet<string> OpenPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Account/Login",
        "/Account/LoginWith2fa",
        "/Account/LoginWithRecoveryCode",
        "/Account/Logout",
        "/Account/Lockout",
        "/Account/AccessDenied",
        "/Account/RecoveryHelp",
        "/Account/ForgotPassword",
        "/Account/ForgotPasswordConfirmation",
        "/Account/ResetPassword",
        "/Account/ResetPasswordConfirmation",
        "/Account/ConfirmEmail",
        "/Account/ConfirmEmailChange",
        "/Account/ResendEmailConfirmation",
        "/Account/ExternalLogin",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!features.CurrentValue.ComingSoon || IsOpen(context))
        {
            await next(context);
            return;
        }

        // A form post that silently becomes a GET of a marketing page reads as success to whoever
        // sent it. Say no plainly instead.
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        // 302, never 301. The curtain comes down at launch, and a permanent redirect is cached by
        // browsers and proxies long after that with no way to un-send it.
        context.Response.Redirect(CurtainFor(context), permanent: false);
    }

    private static bool IsOpen(HttpContext context)
    {
        // Roles ride in the authentication cookie, so this costs no database call. It also means a
        // role removed from an account takes up to the SecurityStampValidator's five minutes to
        // shut the door — which is fine, because this is a marketing curtain and not the thing
        // protecting anything.
        if (Roles.AdminRoles.Any(context.User.IsInRole)) return true;

        var endpoint = context.GetEndpoint();

        // Nothing matched. The curtain covers 404s too, so a stale or guessed link cannot map the
        // site by which paths redirect and which do not.
        if (endpoint is null) return false;

        // No ActionDescriptor means this is not MVC or Razor Pages — in this app, exactly the
        // MapStaticAssets endpoints. They pass: the curtain needs its own stylesheet, fonts and
        // background photograph, and there is no way to allow those without allowing the rest of
        // wwwroot. See the class summary — this is the "curtain, not a lock" line in practice.
        if (endpoint.Metadata.GetMetadata<ActionDescriptor>() is not { } action) return true;

        var values = context.Request.RouteValues;

        // The panel, by area rather than by role, so that an admin whose cookie has expired is sent
        // to sign in by UseAuthorization rather than bounced out to the curtain by us.
        if (string.Equals(values["area"] as string, "Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        return action switch
        {
            // UseExceptionHandler("/Home/Error") re-executes the request through this middleware.
            // Gate it and any exception raised on the curtain becomes 500 -> 302 -> 500, forever.
            ControllerActionDescriptor { ControllerName: "Home", ActionName: "Error" } => true,
            ControllerActionDescriptor controller => OpenControllers.Contains(controller.ControllerName),
            PageActionDescriptor => values["page"] as string is { } page && OpenPages.Contains(page),
            _ => false,
        };
    }

    /// <summary>
    /// Keeps the reader's language across the bounce, so /de/experiences lands on /de/coming-soon
    /// rather than dropping them into English on their way to a page they cannot read.
    ///
    /// The route value is authoritative when there is one. A 404 has no route values at all, so
    /// there the first path segment is checked against the same set of codes the router would have
    /// matched — which is the only place in this class that has to think about URLs.
    /// </summary>
    private static string CurtainFor(HttpContext context)
    {
        if (context.Request.RouteValues["culture"] is string routed && routed.Length > 0)
            return $"/{routed.ToLowerInvariant()}{CurtainPath}";

        var path = context.Request.Path.Value ?? "/";
        var trimmed = path.AsSpan().TrimStart('/');
        var slash = trimmed.IndexOf('/');
        var first = (slash < 0 ? trimmed : trimmed[..slash]).ToString();

        return SiteCultures.UrlCodes.Contains(first, StringComparer.OrdinalIgnoreCase)
            ? $"/{first.ToLowerInvariant()}{CurtainPath}"
            : CurtainPath;
    }
}
