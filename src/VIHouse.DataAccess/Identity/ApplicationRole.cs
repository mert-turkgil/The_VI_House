using Microsoft.AspNetCore.Identity;

namespace VIHouse.DataAccess.Identity;

/// <summary>
/// Roles per brief §96: SuperAdmin, EventManager, Finance, Marketing, Concierge, Support, plus the
/// customer-facing Member/Guest roles. Seeded in DbSeeder.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
