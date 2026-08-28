using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Validation;

/// <summary>
/// Constrains an admin-entered link to something safe to put in an <c>&lt;a href&gt;</c>.
///
/// The href twin of <see cref="SiteImageUrlAttribute"/>, and it exists for the sharper of the two
/// reasons: an <c>href</c> accepts <c>javascript:</c>, so an admin-entered destination rendered
/// into a link is a stored-XSS primitive in a way an <c>img src</c> is not.
///
///   allowed  "/apply"                        site-relative, the common case
///   allowed  "/apply?ref=hero#form"          query and fragment included
///   allowed  "https://vihouse.co/journal"    an explicit https host
///   allowed  "mailto:concierge@vihouse.co"   the one non-http scheme a call to action wants
///   rejected "javascript:…", "data:…"        script in a link
///   rejected "//evil.tld/x"                  protocol-relative — inherits the page scheme
///   rejected "http://…"                      mixed content; the site is https-only in Production
///
/// Empty is valid: a slide with a label and no destination simply renders no button.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class SiteLinkUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string candidate || string.IsNullOrWhiteSpace(candidate))
            return true;

        var url = candidate.Trim();

        if (url.StartsWith("//", StringComparison.Ordinal))
            return false;

        // A site path is allowed, but not one that climbs out of the site — "/../" is normalised by
        // the browser and would leave the origin behind.
        if (url.StartsWith('/'))
            return !url.Contains("..", StringComparison.Ordinal);

        return Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeMailto);
    }

    public override string FormatErrorMessage(string name) =>
        $"{name} must be a site path starting with / (for example /apply), an absolute https:// " +
        "address, or a mailto: address.";
}
