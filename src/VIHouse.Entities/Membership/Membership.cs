using VIHouse.Entities.Common;

namespace VIHouse.Entities.Membership;

/// <summary>
/// Brief §45's Membership object: User/Plan/Start/Renewal/Expiry/Status. One row per purchase —
/// a renewal creates a new row rather than mutating the old one, so history is never lost.
///
/// The one exception to "never mutate" is a recurring plan: the provider bills the same
/// subscription again each period, and a renewal there extends <see cref="ExpiresAt"/> on this row
/// rather than opening a second one, because it is the same purchase continuing — the subscription
/// id is what ties the two together.
/// </summary>
public class Membership : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }

    public DateTimeOffset StartAt { get; set; }

    /// <summary>The next date the provider is expected to charge for a recurring plan; null for a
    /// one-time purchase. Advanced by each renewal webhook.</summary>
    public DateTimeOffset? RenewalAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    /// <summary>The provider's subscription id ("sub_…") for a recurring plan; null for a one-time
    /// purchase. This is the key the renewal and cancellation webhooks match on.</summary>
    public string? ProviderSubscriptionId { get; set; }

    /// <summary>The provider's customer id ("cus_…"), which is what opens the self-service billing
    /// portal for this member. Null when the checkout did not create one (one-time purchases).</summary>
    public string? ProviderCustomerId { get; set; }

    /// <summary>When the provider reported the subscription ended, for a status of Cancelled.</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>True when an admin granted the membership without a purchase — an influencer, a
    /// partner, a make-good. No provider ids, no payment row, and it never renews: it lasts until
    /// ExpiresAt, or for good when that is null.</summary>
    public bool IsComplimentary { get; set; }

    /// <summary>Who granted a complimentary membership, and why — shown on the admin record so
    /// the reason survives staff turnover.</summary>
    public Guid? GrantedByUserId { get; set; }
    public string? GrantNote { get; set; }
}
