namespace VIHouse.DataAccess.Identity;

/// <summary>Well-known role names (brief §96), shared by DbSeeder and every [Authorize(Roles=...)] usage.</summary>
public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string EventManager = "EventManager";
    public const string Finance = "Finance";
    public const string Marketing = "Marketing";
    public const string Concierge = "Concierge";
    public const string Support = "Support";

    /// <summary>Applied to every approved/paid customer, distinct from the admin-side roles above.</summary>
    public const string Member = "Member";

    /// <summary>Brief §47-49: a referral partner with their own login and dashboard — not an admin-side role, not a Member either.</summary>
    public const string Ambassador = "Ambassador";

    public static readonly string[] AdminRoles =
    [
        SuperAdmin, EventManager, Finance, Marketing, Concierge, Support
    ];

    public static readonly string[] All =
    [
        SuperAdmin, EventManager, Finance, Marketing, Concierge, Support, Member, Ambassador
    ];
}
