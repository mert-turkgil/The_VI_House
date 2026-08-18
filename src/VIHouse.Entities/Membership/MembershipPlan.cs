using VIHouse.Entities.Common;

namespace VIHouse.Entities.Membership;

/// <summary>
/// Brief §44-45: membership tiers (Applicant/Guest/Member/Founding Member/Partner/VIP...) are data,
/// not hard-coded — the tier IS the plan's Name, admin-managed, not a C# enum.
/// </summary>
public class MembershipPlan : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public long PriceMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public MembershipBillingPeriod BillingPeriod { get; set; } = MembershipBillingPeriod.Annual;

    /// <summary>Plain text, one feature per line — matches the brief's flat "Features" field; no separate feature-list entity for this MVP.</summary>
    public string? Features { get; set; }

    public MembershipPlanStatus Status { get; set; } = MembershipPlanStatus.Active;
    public int SortOrder { get; set; }
}
