using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Users;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Referrals;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminCustomerDetailViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public List<string> Roles { get; set; } = [];
    public Profile? Profile { get; set; }
    public List<Application> Applications { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    /// <summary>Standalone membership purchases. A separate table from Payment (see
    /// MembershipPayment's own doc comment), so it has to be listed separately or a member who only
    /// ever bought a membership looks like they've never paid for anything.</summary>
    public List<MembershipPayment> MembershipPayments { get; set; } = [];

    // --- Account health -----------------------------------------------------------------------
    // The two questions support actually gets asked ("why can't they log in?", "are they still
    // active?") that previously needed a database query to answer.
    public MemberStatus MemberStatus { get; set; }

    // --- What the panel may do to this account ------------------------------------------------

    /// <summary>Security:ProtectedAccounts — every form on the page is hidden and every POST
    /// refused. See AdminUsersController.RefuseIfProtected.</summary>
    public bool IsProtected { get; set; }

    /// <summary>The current membership, paid or complimentary; null when they hold none.</summary>
    public MembershipSummary? Membership { get; set; }
    public List<MembershipSummary> MembershipHistory { get; set; } = [];

    /// <summary>Plans an admin may grant — the active ones.</summary>
    public List<MembershipPlan> Plans { get; set; } = [];

    public Ambassador? Ambassador { get; set; }
    public AdminMakeAmbassadorViewModel AmbassadorForm { get; set; } = new();
    public bool TwoFactorEnabled { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
