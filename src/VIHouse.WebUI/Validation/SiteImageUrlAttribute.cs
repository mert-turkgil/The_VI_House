using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Validation;

/// <summary>
/// Constrains an admin-entered image URL to something safe to put in an <c>&lt;img src&gt;</c>.
///
/// Cover images are free text — an admin types a path into a box — and that value is rendered
/// straight into markup. Before the move to real <c>&lt;img&gt;</c> elements these values went into
/// <c>style="background-image:url('…')"</c>, where a stray quote could break out of <c>url()</c>
/// into arbitrary CSS. The <c>&lt;img&gt;</c> rewrite removes that particular hole, but it does not
/// remove the underlying fact that an admin can point the site at anything at all, so this narrows
/// it deliberately:
///
///   allowed  "/img/experiences/london-1600.jpg"   site-relative, which is what the fetch script produces
///   allowed  "https://cdn.example.com/x.jpg"      an explicit https host
///   rejected "//evil.tld/x.jpg"                   protocol-relative — inherits the page scheme, easy to miss
///   rejected "http://…"                           mixed content; the site is https-only in Production
///   rejected "data:image/svg+xml;base64,…"        a data URI can carry SVG, and SVG can carry script
///   rejected "/img/../../appsettings.json"        traversal, even though the browser would normalise it
///
/// Empty is valid: a missing cover is a normal state and renders the crest placeholder.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SiteImageUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string candidate || string.IsNullOrWhiteSpace(candidate))
            return true;

        var url = candidate.Trim();

        if (url.Contains("..", StringComparison.Ordinal))
            return false;

        if (url.StartsWith("//", StringComparison.Ordinal))
            return false;

        if (url.StartsWith('/'))
            return true;

        // Anything else has to be an absolute https URL. Uri.TryCreate is what settles whether a
        // string like "javascript:alert(1)" is a scheme we recognise rather than a relative path.
        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps;
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be a site path starting with / (for example /img/experiences/london-1600.jpg) " +
        "or an absolute https:// address.";
}
