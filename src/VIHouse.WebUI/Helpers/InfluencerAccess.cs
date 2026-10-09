using System.Security.Claims;
using VIHouse.DataAccess.Identity;

namespace VIHouse.WebUI.Helpers;

public static class InfluencerAccess
{
    /// <summary>
    /// An influencer with nothing else here — not a paying member, not staff. Their account is the
    /// influencer area: /account sends them there, and the shared account pages (security, data,
    /// notifications) show the influencer menu instead of the member one.
    /// </summary>
    public static bool IsInfluencerOnly(ClaimsPrincipal user) =>
        user.IsInRole(Roles.Ambassador) && !user.IsInRole(Roles.Member) && !Roles.AdminRoles.Any(user.IsInRole);
}
