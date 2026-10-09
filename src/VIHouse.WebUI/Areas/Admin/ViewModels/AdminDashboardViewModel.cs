using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// The dashboard is built per viewer: each panel is filled only when the signed-in admin can open
/// the section behind it (AdminSections.RolesFor), and left null otherwise — an Editor sees the
/// publishing calendar, Finance sees money, Concierge sees new members, and nobody is shown figures
/// for a screen they cannot reach.
/// </summary>
public class AdminDashboardViewModel
{
    public string FirstName { get; set; } = "";
    public IReadOnlyList<string> RoleLabels { get; set; } = [];

    // --- Applications (RolesFor.Applications) ----------------------------------------------------
    public bool CanSeeApplications { get; set; }
    public int PendingApplications { get; set; }
    public int TotalApplications { get; set; }
    public Dictionary<ApplicationStatus, int> ApplicationsByStatus { get; set; } = [];
    public double ConversionToPaidPercent { get; set; }

    // --- Bookings (RolesFor.Bookings) -----------------------------------------------------------
    public bool CanSeeBookings { get; set; }
    public int TotalBookings { get; set; }
    public Dictionary<BookingStatus, int> BookingsByStatus { get; set; } = [];

    /// <summary>Whether the signed-in admin may see money at all (RolesFor.Money). When false,
    /// <see cref="Payments"/> is null and the page shows no revenue or payment figures.</summary>
    public bool CanSeeMoney { get; set; }

    /// <summary>Everything about money, per currency, from the unified transaction table.</summary>
    public PaymentDashboardStats? Payments { get; set; }

    /// <summary>RolesFor.Events — what is coming up, and what is streaming now.</summary>
    public DashboardEventsPanel? Events { get; set; }

    /// <summary>RolesFor.Content — the writing queue.</summary>
    public DashboardContentPanel? Content { get; set; }

    /// <summary>RolesFor.Users — new accounts and the ones that need a hand getting in.</summary>
    public UserDirectoryStats? Users { get; set; }

    /// <summary>RolesFor.Communications — is email actually going out?</summary>
    public DashboardCommsPanel? Comms { get; set; }

    /// <summary>RolesFor.Marketing — launch list and founders.</summary>
    public DashboardMarketingPanel? Marketing { get; set; }
}

public record DashboardEventItem(Guid Id, string Kind, string Title, DateTimeOffset? StartAtUtc, string Status, int Attendees, bool HasStream, bool IsLiveNow);

public class DashboardEventsPanel
{
    public List<DashboardEventItem> Upcoming { get; set; } = [];
    public List<DashboardEventItem> LiveNow { get; set; } = [];
    public int DraftExperiences { get; set; }
    public int DraftSessions { get; set; }
}

public record DashboardArticle(Guid Id, string Title, DateTimeOffset UpdatedAt);

public class DashboardContentPanel
{
    public int Drafts { get; set; }
    public int Published { get; set; }
    public List<DashboardArticle> RecentDrafts { get; set; } = [];
}

public class DashboardCommsPanel
{
    public int FailedEmailsLast7Days { get; set; }
    public int SentEmailsLast7Days { get; set; }
    public IReadOnlyList<string> SmtpProblems { get; set; } = [];
}

public class DashboardMarketingPanel
{
    public int LaunchListSignups { get; set; }
    public int Founders { get; set; }
    public DateTimeOffset? FounderWindowEndsAtUtc { get; set; }
}
