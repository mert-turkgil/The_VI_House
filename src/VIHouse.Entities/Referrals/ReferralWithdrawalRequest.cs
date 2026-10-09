using VIHouse.Entities.Common;

namespace VIHouse.Entities.Referrals;

/// <summary>
/// An influencer asking to be paid what is owed in one currency. It does not move money: Finance
/// sends the bank transfer and then pays the request, which records the ordinary
/// <see cref="ReferralPayout"/> for the whole owed balance (the payout links back here). One open
/// request per influencer and currency.
/// </summary>
public class ReferralWithdrawalRequest : BaseEntity
{
    public Guid AmbassadorId { get; set; }
    public string Currency { get; set; } = default!;

    /// <summary>What was owed when they asked. The payout settles the balance as it stands when
    /// Finance pays, which can differ by then (a sale or a refund in between).</summary>
    public long RequestedMinor { get; set; }

    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Open;

    /// <summary>Anything the influencer wants Finance to know.</summary>
    public string? Note { get; set; }

    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedByAdminId { get; set; }

    /// <summary>The reason given when rejected, or the admin's note when paid.</summary>
    public string? DecisionNote { get; set; }

    public Guid? PayoutId { get; set; }
}

public enum WithdrawalStatus
{
    Open,
    Paid,
    Rejected,
    Cancelled,
}
