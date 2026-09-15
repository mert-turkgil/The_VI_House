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

    /// <summary>What the member's tier opens; null for a guest, prospect or staff. The view gates
    /// the perks list on this rather than on "has a membership".</summary>
    public MemberEntitlements? Entitlements { get; set; }

    /// <summary>
    /// One card per online thing the person holds a place on — an experience they have a ticket
    /// for, a session they are enrolled on — with its live stream, meeting room and community
    /// links. This is how a ticket holder without a membership still gets a door to what they
    /// bought, and how a member finds tonight's session without hunting.
    /// </summary>
    public List<AccessHub> Hubs { get; set; } = [];

    /// <summary>Set when the signed-in person is an ambassador: their link, for the panel on the
    /// dashboard. Full stats live on /ambassador.</summary>
    public string? ReferralCode { get; set; }
}

public record AccessHub(
    string Kind,
    string Title,
    string Href,
    DateTimeOffset? StartAtUtc,
    DateTimeOffset? EndAtUtc,
    string? LiveStreamUrl,
    string? MeetingUrl,
    List<VIHouse.Entities.Community.CommunityLink> Links)
{
    /// <summary>Within the window the stream is worth embedding: a quarter-hour before the start
    /// until the end (or two hours after the start when no end is set).</summary>
    public bool IsLiveNow
    {
        get
        {
            if (StartAtUtc is not { } start) return LiveStreamUrl is not null; // on demand
            var now = DateTimeOffset.UtcNow;
            return now >= start.AddMinutes(-15) && now <= (EndAtUtc ?? start.AddHours(2));
        }
    }

    public bool HasAnythingToOpen => LiveStreamUrl is not null || MeetingUrl is not null || Links.Count > 0;
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
