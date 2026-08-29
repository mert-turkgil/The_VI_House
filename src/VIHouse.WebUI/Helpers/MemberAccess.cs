using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// The one answer to "is this person a member of The VI House?".
///
/// It matters because the site has two things that sound the same and are not. The <c>Member</c>
/// role is granted to anyone who completes a payment — including someone who bought a single
/// ticket to one experience — because that is what provisions their account
/// (see PaymentService). An active <c>Membership</c> row is what actually makes someone a member of
/// the House.
///
/// Checking the role where membership was meant is how a ticket-holder ended up seeing the member
/// directory in the nav, reaching /members directly, and counting as a member for members-only
/// sessions. Views/Account/_AccountNav.cshtml had it right and said so in a comment; this puts that
/// same test in one place so no screen has to re-derive it.
///
/// The check costs a database read, so it is for page-level decisions — a nav link, an
/// authorisation gate — not for something called per row in a loop.
/// </summary>
public static class MemberAccess
{
    /// <summary>
    /// True when the signed-in person holds an active membership. False for an anonymous visitor and
    /// for a ticket-holder, which is the distinction the whole helper exists to make.
    /// </summary>
    public static async Task<bool> HasActiveMembershipAsync(
        ClaimsPrincipal user,
        IMembershipService membershipService,
        UserManager<ApplicationUser> userManager,
        CancellationToken ct = default)
    {
        if (user.Identity?.IsAuthenticated != true) return false;

        return userManager.GetUserId(user) is { } id
            && Guid.TryParse(id, out var userId)
            && await membershipService.GetCurrentMembershipAsync(userId, ct) is not null;
    }
}
