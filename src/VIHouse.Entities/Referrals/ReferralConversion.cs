using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// One row per thing that happened because of a referral — the ledger the ambassador's timeline
/// reads, and the money ledger commission is paid from: each purchase line keeps the rate in force
/// when it happened, gives commission back when the purchase is refunded, and points at the payout
/// that settled it. Keyed on the source row so a replayed webhook cannot write it twice. Deliberately carries no name or email: brief §49 keeps customer data away from
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

    /// <summary>The experience or session this conversion is about, when it is about one — an
    /// application's experience, a ticket's experience, a session purchase's seminar. Lets the
    /// timeline name it and the per-link table count it.</summary>
    public ReferralTargetKind TargetKind { get; set; } = ReferralTargetKind.Site;
    public Guid? TargetId { get; set; }

    // --- Refunds and voids -----------------------------------------------------------------------

    /// <summary>How much of <see cref="AmountMinor"/> has gone back to the buyer, as the payment
    /// provider reports it (cumulative). 0 until a refund lands.</summary>
    public long RefundedMinor { get; set; }

    /// <summary>The part of <see cref="CommissionMinor"/> that no longer stands — pro rata to a
    /// partial refund, all of it on a full refund or a void. The ambassador earns
    /// CommissionMinor − CommissionReversedMinor.</summary>
    public long CommissionReversedMinor { get; set; }

    /// <summary>When the purchase was fully refunded — the conversion no longer counts.</summary>
    public DateTimeOffset? ReversedAt { get; set; }

    /// <summary>An admin struck the commission out (self-referral, fraud, goodwill...).</summary>
    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedByAdminId { get; set; }
    public string? VoidReason { get; set; }

    // --- Payout ----------------------------------------------------------------------------------

    /// <summary>The payout this line was settled in; null while it is still owed.</summary>
    public Guid? PayoutId { get; set; }

    // --- Fraud signals ---------------------------------------------------------------------------

    /// <summary>The buyer's account is the ambassador's own. Flagged, not blocked: an admin decides
    /// whether to void it before the payout.</summary>
    public bool IsSelfReferral { get; set; }

    /// <summary>What the ambassador still earns from this line.</summary>
    public long NetCommissionMinor => Math.Max(0, (CommissionMinor ?? 0) - CommissionReversedMinor);
}
