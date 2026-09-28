using System.Security.Claims;

namespace VIHouse.WebUI.Helpers;

/// <summary>The signed-in account's id, read the same way everywhere (Identity's default
/// user-id claim).</summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? UserId(this ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>For endpoints behind [Authorize], where a missing id is a bug, not a state.</summary>
    public static Guid RequiredUserId(this ClaimsPrincipal user) =>
        user.UserId() ?? throw new InvalidOperationException("No signed-in user on an endpoint that requires one.");
}
