using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// One row per thing that happened because of a referral — the ledger the ambassador's timeline
/// reads. Totals (AmbassadorStats) are still computed live from the applications and payments
/// themselves; this table exists so the ambassador can be shown <em>when</em> each step happened
/// and told about it once, and it is keyed on the source row so a replayed webhook cannot write
/// it twice. Deliberately carries no name or email: brief §49 keeps customer data away from
/// partners.
/// </summary>
public class ReferralConversion : BaseEntity
{
    public Guid AmbassadorId { get; set; }
    public ReferralConversionKind Kind { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>What was paid, for the purchase kinds; null for an application or approval.</summary>
    public long? AmountMinor { get; set; }
    public string? Currency { get; set; }

    /// <summary>The ambassador's share at the rate in force when it happened.</summary>
    public long? CommissionMinor { get; set; }

    /// <summary>The row this came from — "Application", "Payment", "MembershipPayment" — and its id.</summary>
    public string SourceEntityType { get; set; } = default!;
    public Guid SourceEntityId { get; set; }
}
