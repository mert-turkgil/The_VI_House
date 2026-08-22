using VIHouse.DataAccess.Identity;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Seed;

/// <summary>
/// A bootstrap admin account defined in configuration. This exists to solve a chicken-and-egg
/// problem: the Invite Admin screen needs an existing SuperAdmin to sign in and use it, so the very
/// first account on a fresh deployment has to come from somewhere else.
///
/// Credentials live in user-secrets (Development) or the gitignored appsettings.Production.json on
/// the server — never a committed file.
/// </summary>
public record SeedAdminAccount(
    string Email,
    string Password,
    string? FirstName = null,
    string? LastName = null,
    string[]? Roles = null)
{
    /// <summary>Defaults to SuperAdmin: a bootstrap account that couldn't manage roles would leave
    /// nobody able to grant them, which is the situation this type exists to avoid.</summary>
    public string[] EffectiveRoles =>
        Roles is { Length: > 0 } ? Roles.Intersect(Identity.Roles.AdminRoles).ToArray() : [Identity.Roles.SuperAdmin];
}
