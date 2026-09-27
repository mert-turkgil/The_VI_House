using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace VIHouse.WebUI.Services;

/// <summary>
/// Proof that this browser submitted an application, so /apply/status can show it without an
/// account and without a secret in the URL.
///
/// The status page used to be /apply/status/{applicationId}: the id was printed on the "application
/// received" page as a link to bookmark, and anyone holding it — a forwarded mail, a shared screen,
/// a browser history — saw the applicant's name and decision. Now the ids live in an encrypted,
/// HttpOnly cookie written when the form is posted: unreadable and unforgeable from script or by
/// hand (Data Protection), sent only to this site, and gone with the browser's cookies. A signed-in
/// applicant does not need it at all — their own applications are found through the account.
/// </summary>
public sealed class ApplicationStatusCookie(IDataProtectionProvider dataProtection)
{
    public const string Name = "vih_apps";
    private const int MaxApplications = 10;
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(180);

    private readonly IDataProtector protector = dataProtection.CreateProtector("VIHouse.ApplicationStatusCookie.v1");

    public IReadOnlyList<Guid> Read(HttpRequest request)
    {
        if (!request.Cookies.TryGetValue(Name, out var raw) || string.IsNullOrEmpty(raw)) return [];

        try
        {
            return protector.Unprotect(raw)
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
        }
        catch (CryptographicException)
        {
            // Tampered with, or written under a key this server no longer has: worth nothing, and
            // not an error — the page just shows no applications for this browser.
            return [];
        }
    }

    public void Add(HttpContext context, Guid applicationId)
    {
        var ids = Read(context.Request).Where(id => id != applicationId).Prepend(applicationId).Take(MaxApplications);

        context.Response.Cookies.Append(Name, protector.Protect(string.Join(',', ids)), new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.Add(Lifetime),
            // The applicant's own request for their own record — the same footing as the session
            // cookie, not tracking.
            IsEssential = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
        });
    }
}
