namespace VIHouse.WebUI.Services;

/// <summary>
/// The referral attribution cookie: the ambassador code, set by ReferralController's /r/{code}…
/// redirects and read by ApplicationController (pre-fills the apply form), MembershipController and
/// JoinController (attached to the resulting MembershipPayment) and SeminarsController (attached to
/// the enrolment). Only the code travels in the cookie — which experience or session a conversion
/// is about comes from the application, payment or enrolment row itself, which cannot be wrong.
/// </summary>
public static class ReferralCookie
{
    public const string Name = "vih_ref";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    public static void Write(HttpResponse response, string code) =>
        response.Cookies.Append(Name, code, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.Add(Lifetime),
            IsEssential = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = response.HttpContext.Request.IsHttps,
        });
}
