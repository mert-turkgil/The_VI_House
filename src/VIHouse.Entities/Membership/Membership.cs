using VIHouse.Entities.Common;

namespace VIHouse.Entities.Membership;

/// <summary>
/// Brief §45's Membership object: User/Plan/Start/Renewal/Expiry/Status. One row per purchase —
/// a renewal creates a new row rather than mutating the old one, so history is never lost.
/// </summary>
public class Membership : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }

    public DateTimeOffset StartAt { get; set; }

    /// <summary>Stored for future auto-renewal billing (brief §46, explicitly a later phase) — nothing reads this to auto-charge yet.</summary>
    public DateTimeOffset? RenewalAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;
}
