using VIHouse.Entities.Commerce;
using VIHouse.Entities.Common;

namespace VIHouse.Entities.Membership;

/// <summary>
/// A standalone-membership purchase's Stripe Checkout Session record — deliberately NOT the same
/// table as Commerce.Payment: Payment's ApplicationId/ExperienceId/TicketTypeId are all non-nullable,
/// and a membership purchase has none of those. Reuses Commerce.PaymentStatus since that's a plain
/// status enum with no FK coupling to Payment itself.
/// </summary>
public class MembershipPayment : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public Guid? MembershipId { get; set; }

    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public PaymentStatus Status { get; set; } = PaymentStatus.Created;

    public string ProviderReference { get; set; } = default!;
}
