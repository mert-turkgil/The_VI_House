using Microsoft.AspNetCore.Mvc.Filters;

namespace VIHouse.WebUI.Filters;

/// <summary>
/// Keeps the pages that have no business in a search result out of one, without each of them having
/// to remember.
///
/// Applied globally and self-scoping, the way OnboardingRequirementFilter is: it looks at the path
/// and does nothing to the public site. Two categories get it —
///
///   - anything requiring a signed-in visitor (account, checkout, onboarding, the member directory).
///     These already redirect a crawler to a login, but a redirect chain is not a signal, and a
///     stray public link into one of them should not put it in the index.
///   - search results and other pages that generate unbounded near-duplicate URLs from a query
///     string. This is the standard way a site spends its crawl budget on nothing.
///
/// Nothing here blocks a person. A noindex page is fully readable; it simply asks not to be listed.
///
/// The flag goes in HttpContext.Items rather than ViewData, because the Identity screens are Razor
/// Pages: MvcOptions filters run for them too, but their "controller" is a PageModel, so a cast to
/// Controller silently skipped every login, register and 2FA page — the exact pages this exists to
/// cover. HttpContext.Items is the one bag both kinds of endpoint share.
/// </summary>
public class NoIndexFilter : IResultFilter
{
    /// <summary>Read by Views/Shared/_Seo.cshtml.</summary>
    public const string ItemKey = "Seo.NoIndex";

    private static readonly string[] Paths =
    [
        "/account", "/checkout", "/onboarding", "/identity", "/invitation",
        "/apply", "/join", "/members", "/ambassador", "/r/", "/search",
    ];

    public void OnResultExecuting(ResultExecutingContext context)
    {
        var path = context.HttpContext.Request.Path.Value;
        if (string.IsNullOrEmpty(path)) return;

        // The language prefix comes off first, or /de/account would be indexable while /account was
        // not — the kind of gap that only surfaces once it is already in the index.
        var bare = Helpers.SeoResolver.StripCulture(path);

        if (Paths.Any(p => bare.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            context.HttpContext.Items[ItemKey] = true;
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
