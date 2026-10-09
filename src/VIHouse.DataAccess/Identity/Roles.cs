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

    /// <summary>Writes and publishes content — experiences, sessions, journal, CMS pages, hero
    /// slides — and nothing operational: no applications, money, or accounts.</summary>
    public const string Editor = "Editor";

    /// <summary>Applied to every approved/paid customer, distinct from the admin-side roles above.</summary>
    public const string Member = "Member";

    /// <summary>Brief §47-49: a referral partner — shown to people as an <em>influencer</em> — with their
    /// own login and area. Not an admin-side role, not a Member either.</summary>
    public const string Ambassador = "Ambassador";

    public static readonly string[] AdminRoles =
    [
        SuperAdmin, Editor, EventManager, Finance, Marketing, Concierge, Support
    ];

    public static readonly string[] All =
    [
        SuperAdmin, Editor, EventManager, Finance, Marketing, Concierge, Support, Member, Ambassador
    ];

    /// <summary>Who is told about an influencer's journal submission — the roles the admin panel's
    /// Journal section admits (AdminSections.RolesFor.Content).</summary>
    public static readonly string[] JournalReviewers = [SuperAdmin, Editor, Marketing];

    /// <summary>Who is told about an influencer's withdrawal request — the roles that can pay one
    /// (AdminSections.RolesFor.Money).</summary>
    public static readonly string[] PayoutApprovers = [SuperAdmin, Finance];
}
