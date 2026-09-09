using Microsoft.AspNetCore.Mvc;
using VIHouse.WebUI.ViewModels.Seo;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// One line per page, so that giving a page its own description does not mean building a PageSeo by
/// hand at every call site.
///
/// Worth doing for the listing pages specifically: without it they all inherit the site-wide
/// default description, and a set of pages sharing one description is a set Google discards the
/// description for, writing its own snippet from the page text instead. That snippet is rarely the
/// sentence you would have chosen.
/// </summary>
public static class SeoControllerExtensions
{
    /// <summary>
    /// Merges into whatever is already in ViewData rather than replacing it, so this can be called
    /// alongside a richer PageSeo built elsewhere without either overwriting the other.
    /// </summary>
    public static void SetSeo(
        this Controller controller,
        string? description = null,
        string? canonicalPath = null,
        string? title = null,
        bool titleIsComplete = false,
        params SeoBreadcrumb[] breadcrumbs)
    {
        var seo = controller.ViewData["Seo"] as PageSeo ?? new PageSeo();

        if (description is not null) seo.Description = description;
        if (canonicalPath is not null) seo.CanonicalPath = canonicalPath;

        if (title is not null)
        {
            seo.Title = title;
            seo.TitleIsComplete = titleIsComplete;
        }

        if (breadcrumbs.Length > 0) seo.Breadcrumbs = [.. breadcrumbs];

        controller.ViewData["Seo"] = seo;
    }

    /// <summary>
    /// Copy that loses to the settings screen rather than beating it.
    ///
    /// The homepage uses this: its resource-file title and description are what a fresh install
    /// shows, and anything typed into Site &amp; SEO replaces them. Passing them as Title and
    /// Description instead would make that screen appear broken on the one page it matters most on.
    /// </summary>
    public static void SetSeoFallbacks(
        this Controller controller,
        string? titleFallback = null,
        string? descriptionFallback = null,
        string? canonicalPath = null)
    {
        var seo = controller.ViewData["Seo"] as PageSeo ?? new PageSeo();

        if (titleFallback is not null) seo.TitleFallback = titleFallback;
        if (descriptionFallback is not null) seo.DescriptionFallback = descriptionFallback;
        if (canonicalPath is not null) seo.CanonicalPath = canonicalPath;

        controller.ViewData["Seo"] = seo;
    }
}
