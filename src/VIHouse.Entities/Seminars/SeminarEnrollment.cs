using VIHouse.Entities.Common;

namespace VIHouse.Entities.Seminars;

/// <summary>
/// One person's place on one seminar, and the record of how they got it. Exactly one row exists per
/// (Seminar, User) pair — a retry after an abandoned checkout reuses this row rather than adding a
/// second, which is what lets the unique index below stand as the real guard against a double
/// enrolment racing in from two tabs.
///
/// Confirmed is the only status that grants access to the body and media.
/// </summary>
public class SeminarEnrollment : BaseEntity
{
    public Guid SeminarId { get; set; }
    public Guid UserId { get; set; }

    public SeminarEnrollmentStatus Status { get; set; } = SeminarEnrollmentStatus.Pending;
    public SeminarAccessGrant GrantedVia { get; set; }

    /// <summary>What was actually charged, captured at enrolment — a later price change on the
    /// seminar must not rewrite what someone paid.</summary>
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";

    /// <summary>The payment provider's checkout session id, and the key the webhook matches on.
    /// Null for an enrolment that never involved a payment (free, or covered by membership).</summary>
    public string? ProviderReference { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
}
