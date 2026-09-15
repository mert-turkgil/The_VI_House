using System.Security.Claims;
using VIHouse.DataAccess.Identity;

namespace VIHouse.WebUI.Areas.Admin;

/// <summary>
/// The one table that says which admin roles may open which part of the panel. The sidebar
/// (_AdminLayout.cshtml) reads it to decide what to draw, and every controller declares its
/// section's roles through the constants in <see cref="RolesFor"/> — so the menu and the
/// authorization can never disagree, and a section that isn't listed here cannot be reached.
///
/// SuperAdmin is on every row. The other roles follow brief §96: Editor writes content and touches
/// nothing operational; EventManager runs the events; Finance sees money; Marketing owns the
/// public face of the site; Concierge and Support handle people.
/// </summary>
public static class AdminSections
{
    public sealed record Section(string Controller, string Action, string Label, string Icon, string[] Roles)
    {
        public string RolesCsv => string.Join(',', Roles);
        public bool IsVisibleTo(ClaimsPrincipal user) => Roles.Any(user.IsInRole);
    }

    public sealed record QuickAction(string Controller, string Action, string Label);

    // --- Role groups ------------------------------------------------------------------------------
    // Declared as constants so [Authorize(Roles = ...)] can use them; attributes need compile-time
    // strings. The arrays below are built from the same values.

    public static class RolesFor
    {
        public const string Everyone = $"{Roles.SuperAdmin},{Roles.Editor},{Roles.EventManager},{Roles.Finance},{Roles.Marketing},{Roles.Concierge},{Roles.Support}";
        public const string Applications = $"{Roles.SuperAdmin},{Roles.EventManager},{Roles.Concierge},{Roles.Support}";
        public const string Users = $"{Roles.SuperAdmin},{Roles.Concierge},{Roles.Support}";
        public const string Events = $"{Roles.SuperAdmin},{Roles.Editor},{Roles.EventManager}";
        public const string Content = $"{Roles.SuperAdmin},{Roles.Editor},{Roles.Marketing}";
        public const string Bookings = $"{Roles.SuperAdmin},{Roles.EventManager},{Roles.Finance},{Roles.Concierge},{Roles.Support}";
        public const string Money = $"{Roles.SuperAdmin},{Roles.Finance}";
        public const string Marketing = $"{Roles.SuperAdmin},{Roles.Marketing}";
        public const string Communications = $"{Roles.SuperAdmin},{Roles.Marketing},{Roles.Concierge},{Roles.Support}";
    }

    private static string[] Split(string csv) => csv.Split(',', StringSplitOptions.RemoveEmptyEntries);

    // --- Sidebar ----------------------------------------------------------------------------------
    // Order is the order in the sidebar.

    public static readonly IReadOnlyList<Section> All =
    [
        new("AdminDashboard", "Index", "Dashboard", "layout-dashboard", Split(RolesFor.Everyone)),
        new("AdminApplications", "Index", "Applications", "file-text", Split(RolesFor.Applications)),
        new("AdminUsers", "Index", "Users", "user-round", Split(RolesFor.Users)),
        new("AdminExperiences", "Index", "Experiences", "sparkles", Split(RolesFor.Events)),
        new("AdminSeminars", "Index", "Sessions", "calendar", Split(RolesFor.Events)),
        new("AdminBookings", "Index", "Bookings", "calendar", Split(RolesFor.Bookings)),
        new("AdminPayments", "Index", "Payments", "credit-card", Split(RolesFor.Money)),
        new("AdminPromoCodes", "Index", "Promo Codes", "tag", Split(RolesFor.Money)),
        new("AdminMembershipPlans", "Index", "Membership Plans", "layers", Split(RolesFor.Money)),
        new("AdminAmbassadors", "Index", "Ambassadors", "megaphone", Split(RolesFor.Marketing)),
        new("AdminJournal", "Index", "Journal", "book-open", Split(RolesFor.Content)),
        new("AdminCms", "Index", "Content", "pen-square", Split(RolesFor.Content)),
        new("AdminHeroSlides", "Index", "Hero Slides", "image", Split(RolesFor.Content)),
        new("AdminCommunity", "Index", "Community", "messages-square", Split(RolesFor.Marketing)),
        new("AdminEmails", "Index", "Emails & SMS", "mail", Split(RolesFor.Communications)),
        new("AdminNotifySignups", "Index", "Launch List", "bell", Split(RolesFor.Marketing)),
        new("AdminTranslations", "Index", "Translations", "languages", Split(RolesFor.Marketing)),
        new("AdminSiteSettings", "Index", "Site & SEO", "settings", Split(RolesFor.Marketing)),
    ];

    private static readonly IReadOnlyList<QuickAction> QuickActions =
    [
        new("AdminApplications", "Index", "Review queue"),
        new("AdminExperiences", "Create", "New experience"),
        new("AdminSeminars", "Create", "New session"),
        new("AdminJournal", "Create", "New article"),
    ];

    public static IEnumerable<Section> VisibleTo(ClaimsPrincipal user) => All.Where(s => s.IsVisibleTo(user));

    /// <summary>Quick actions whose target section the user may open — a "New article" button
    /// for someone who cannot reach the journal would only lead to an access-denied page.</summary>
    public static IEnumerable<QuickAction> QuickActionsFor(ClaimsPrincipal user)
    {
        var visible = VisibleTo(user).Select(s => s.Controller).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return QuickActions.Where(q => visible.Contains(q.Controller));
    }

    public static Section? Find(string? controller) =>
        All.FirstOrDefault(s => string.Equals(s.Controller, controller, StringComparison.OrdinalIgnoreCase));
}
