using VIHouse.Business.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Seminars;

namespace VIHouse.WebUI.ViewModels.Account;

/// <summary>
/// Who the signed-in person is to the House. Decides which account experience they get: the
/// member dashboard, the guest view (a ticket or a session but no membership), or the staff view.
/// A person can be several of these — an admin who also pays for membership — so the flags are
/// independent and the view reads them in the order that matters to it.
/// </summary>
public enum AccountStanding
{
    /// <summary>Signed in, but no membership, no booking and no session — an account with nothing on it yet.</summary>
    Prospect,

    /// <summary>Holds a ticket to an experience or a place on a session, but no membership.</summary>
    Guest,

    /// <summary>Holds an active membership.</summary>
    Member,

    /// <summary>Holds an admin-side role.</summary>
    Staff,
}

public class AccountDashboardViewModel
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public AccountStanding Standing { get; set; }

    /// <summary>Null when there is no current membership; the view then offers the plans.</summary>
    public MembershipSummary? Membership { get; set; }

    /// <summary>True when the provider can open a billing portal for this member — a recurring
    /// plan with a customer record behind it.</summary>
    public bool CanManageBilling { get; set; }

    /// <summary>Purely presentational — derived from the Membership row's own id (see Card).</summary>
    public string? MemberNumber { get; set; }

    public bool ProfileComplete { get; set; }
    public int UnreadNotifications { get; set; }

    // --- What is coming up ---------------------------------------------------------------------

    public List<DashboardSessionItem> UpcomingSessions { get; set; } = [];
    public int OnDemandSessionCount { get; set; }
    public int TotalSessionCount { get; set; }

    public List<DashboardBookingItem> UpcomingBookings { get; set; } = [];
    public int TotalBookingCount { get; set; }

    /// <summary>The plans on offer, for a guest or prospect while membership is on sale.</summary>
    public List<Membership.MembershipPlanCardViewModel> Plans { get; set; } = [];

    public bool CommunityEnabled { get; set; }
    public bool DirectoryEnabled { get; set; }
}

public record DashboardSessionItem(
    string Slug,
    string Title,
    DateTimeOffset? StartAtUtc,
    bool IsOnline,
    string? Location,
    SeminarAccessGrant GrantedVia,
    bool HasMeetingLink);

public record DashboardBookingItem(
    string Reference,
    string ExperienceLabel,
    DateTimeOffset StartAtUtc,
    BookingStatus Status);
