using VIHouse.Entities.Commerce;

namespace VIHouse.Business.Abstract;

/// <summary>
/// The read side of the payment model for the admin area: dashboard figures and the unified
/// transaction list/detail, all from <c>PaymentTransactions</c> — the one table every flow's
/// money goes through — so an experience ticket, a session seat and a membership are counted
/// the same way and a webhook that moves a row is visible here on the next request.
///
/// Money is never summed across currencies. Every figure is per currency.
/// </summary>
public interface IPaymentReportingService
{
    Task<PaymentDashboardStats> GetDashboardStatsAsync(CancellationToken ct = default);
    Task<PaymentTransactionPage> ListTransactionsAsync(PaymentTransactionQuery query, CancellationToken ct = default);
    Task<PaymentTransactionDetail?> GetTransactionAsync(Guid id, CancellationToken ct = default);

    /// <summary>Older admin links point at a Payment / MembershipPayment row id. Finds the
    /// transaction that row belongs to, if any.</summary>
    Task<Guid?> ResolveTransactionIdAsync(Guid rowId, CancellationToken ct = default);
}

/// <summary>One currency's money: what settled (gross), what went back, and the difference.</summary>
public record CurrencyMoney(string Currency, long GrossMinor, long RefundedMinor, int Count)
{
    public long NetMinor => GrossMinor - RefundedMinor;
}

public record KindCurrencyMoney(PaymentTransactionKind Kind, string Currency, int Count, long GrossMinor, long RefundedMinor)
{
    public long NetMinor => GrossMinor - RefundedMinor;
}

public record MonthlyMoneyPoint(string Label, long AmountMinor);

public class PaymentDashboardStats
{
    /// <summary>Every transaction, by internal status — the nine states, nothing derived.</summary>
    public Dictionary<PaymentTransactionStatus, int> CountsByStatus { get; init; } = [];
    public Dictionary<PaymentTransactionKind, int> CountsByKind { get; init; } = [];

    /// <summary>Settled money per currency: Succeeded, PartiallyRefunded and Refunded rows count as
    /// collected (the money did arrive); their refunded amounts are subtracted for the net.</summary>
    public List<CurrencyMoney> CollectedByCurrency { get; init; } = [];
    public List<KindCurrencyMoney> CollectedByKindAndCurrency { get; init; } = [];

    /// <summary>Money still in flight per currency: Pending, Processing and RequiresAction rows.</summary>
    public List<CurrencyMoney> OpenByCurrency { get; init; } = [];

    /// <summary>Net collected per calendar month for the trailing 12 months, per currency, keyed
    /// on the date the provider confirmed the money (PaidAt), not the checkout start.</summary>
    public Dictionary<string, List<MonthlyMoneyPoint>> MonthlyNetByCurrency { get; init; } = [];

    /// <summary>The currency carrying the most net revenue — what the single trend line shows.</summary>
    public string? PrimaryCurrency { get; init; }

    public int PendingCount => CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.Pending);
    public int ProcessingCount => CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.Processing) + CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.RequiresAction);
    public int SucceededCount => CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.Succeeded);
    public int FailedCount => CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.Failed);
    public int CanceledCount => CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.Canceled) + CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.Expired);
    public int RefundedCount => CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.Refunded) + CountsByStatus.GetValueOrDefault(PaymentTransactionStatus.PartiallyRefunded);
    public int DisputedCount { get; init; }

    /// <summary>Memberships sold: settled first payments (not renewals).</summary>
    public int MembershipSales { get; init; }
    /// <summary>Renewals settled.</summary>
    public int MembershipRenewals { get; init; }
    /// <summary>People who hold a membership right now (Active or PastDue, not lapsed).</summary>
    public int CurrentMembers { get; init; }
}

public record PaymentTransactionQuery(
    PaymentTransactionStatus? Status = null,
    PaymentTransactionKind? Kind = null,
    string? Currency = null,
    string? Search = null,
    int Take = 200);

public class PaymentTransactionListItem
{
    public Guid Id { get; init; }
    public PaymentTransactionKind Kind { get; init; }
    public PaymentTransactionStatus Status { get; init; }
    public long AmountMinor { get; init; }
    public long AmountRefundedMinor { get; init; }
    public string Currency { get; init; } = default!;
    public Guid? UserId { get; init; }
    public string? UserEmail { get; init; }
    public string? UserName { get; init; }
    /// <summary>What was bought, in words: "The VI House — Lisbon · VI-26-0042", "Member Monthly", a session title.</summary>
    public string RelatedLabel { get; init; } = "—";
    public string RelatedEntityType { get; init; } = default!;
    public Guid RelatedEntityId { get; init; }
    /// <summary>The admin page for the thing bought, when there is one: a booking, a session's enrolments, a plan.</summary>
    public string? RelatedAdminPath { get; init; }
    public string? ProviderSessionId { get; init; }
    public string? ProviderPaymentIntentId { get; init; }
    public string? ProviderSubscriptionId { get; init; }
    public string? ProviderInvoiceId { get; init; }
    public string? ProviderCustomerId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public DateTimeOffset? PaidAt { get; init; }
    public DateTimeOffset? RefundedAt { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
    public bool Disputed { get; init; }
    public string? LastEventId { get; init; }

    /// <summary>"Refunded 20.00 of 60.00" — or nothing when no money went back.</summary>
    public bool HasRefund => AmountRefundedMinor > 0;
}

public record PaymentTransactionPage(List<PaymentTransactionListItem> Rows, Dictionary<PaymentTransactionStatus, int> CountsByStatus, int Total, List<string> Currencies);

public class PaymentTransactionDetail
{
    public PaymentTransactionListItem Transaction { get; init; } = default!;
    /// <summary>Every provider event recorded against this transaction's provider objects, newest first.</summary>
    public List<WebhookEvent> Events { get; init; } = [];
    /// <summary>What the provider says right now about the checkout session, when there is one and it can be reached.</summary>
    public PaymentProviderDetails? LiveDetails { get; init; }
}
