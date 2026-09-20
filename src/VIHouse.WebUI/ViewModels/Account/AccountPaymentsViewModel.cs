using VIHouse.Business.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.ViewModels.Account;

/// <summary>
/// The member's own payment record. The rows are the same PaymentTransactions the admin sees —
/// one table, one truth — projected by <see cref="IPaymentReportingService.ListForUserAsync"/> to
/// what is theirs to know: no provider ids, no card details, nothing about disputes.
/// </summary>
public class AccountPaymentsViewModel
{
    public List<MemberPaymentItem> Payments { get; init; } = [];

    /// <summary>Money still moving — the provider has not said yes or no yet. Shown first, because
    /// it is the only part of the list where something might still be expected of the member.</summary>
    public List<MemberPaymentItem> InFlight => Payments.Where(p => p.Status
        is PaymentTransactionStatus.Pending or PaymentTransactionStatus.Processing or PaymentTransactionStatus.RequiresAction).ToList();

    public List<MemberPaymentItem> Settled => Payments.Except(InFlight).ToList();

    /// <summary>What was actually collected, per currency and net of refunds. Currencies are never
    /// added together — the House sells in more than one and a combined figure would be fiction.</summary>
    public List<(string Currency, long NetMinor)> TotalsByCurrency => Payments
        .Where(p => p.Status is PaymentTransactionStatus.Succeeded or PaymentTransactionStatus.PartiallyRefunded or PaymentTransactionStatus.Refunded)
        .GroupBy(p => p.Currency)
        .Select(g => (g.Key, g.Sum(p => p.AmountMinor - p.AmountRefundedMinor)))
        .Where(t => t.Item2 > 0)
        .OrderByDescending(t => t.Item2)
        .ToList();
}
