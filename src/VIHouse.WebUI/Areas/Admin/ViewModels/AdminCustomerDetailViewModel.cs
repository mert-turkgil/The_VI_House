using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Users;

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
    public bool TwoFactorEnabled { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
