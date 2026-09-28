using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// One settlement of an ambassador's commission: the House paid them this much, in this
/// currency, on this day. The ledger lines it covers point back at it (ReferralConversion.PayoutId).
/// What is owed is always "net commission on the ledger − sum of payouts", so a refund landing
/// after a payout shows up as a negative balance (to recover or net off the next payout) rather
/// than silently rewriting a payment that has already been made.
/// </summary>
public class ReferralPayout : BaseEntity
{
    public Guid AmbassadorId { get; set; }
    public string Currency { get; set; } = default!;
    public long AmountMinor { get; set; }
    public DateTimeOffset PaidAt { get; set; }
    public Guid PaidByAdminId { get; set; }

    /// <summary>Bank transfer reference, invoice number — whatever finds the payment again.</summary>
    public string? Reference { get; set; }
    public string? Note { get; set; }
}
