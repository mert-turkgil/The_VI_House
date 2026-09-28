using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.Business.Concrete;

public class CheckoutReconciliationService(
    IPaymentTransactionRepository transactions,
    IPaymentProvider paymentProvider,
    IPaymentWebhookDispatcher dispatcher,
    IWebhookEventRepository webhookEvents,
    ILogger<CheckoutReconciliationService> logger) : ICheckoutReconciliationService
{
    /// <summary>One-off sessions expire at the provider after 30 minutes; a Pending row older than
    /// this either has an answer or belongs to a subscription session (24 h), which is re-read at
    /// this cadence until it does.</summary>
    private static readonly TimeSpan PendingAfter = TimeSpan.FromMinutes(35);

    /// <summary>A completed checkout waiting on a bank. The provider sends the outcome as an event
    /// when it knows; this is the safety net for an event that never arrived.</summary>
    private static readonly TimeSpan ProcessingAfter = TimeSpan.FromHours(6);

    private const int SweepBatch = 50;

    private static readonly PaymentTransactionStatus[] Open = [PaymentTransactionStatus.Pending, PaymentTransactionStatus.Processing];

    public async Task<CheckoutReconcileOutcome> ReconcileSessionAsync(string sessionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || !sessionId.StartsWith("cs_", StringComparison.Ordinal) || sessionId.Length > 100)
            return CheckoutReconcileOutcome.Unknown;

        var transaction = await transactions.GetBySessionAsync(sessionId, ct);
        if (transaction is null) return CheckoutReconcileOutcome.Unknown;

        return await ReconcileAsync(transaction, ct);
    }

    public async Task<int> ReconcileForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var settled = 0;
        foreach (var transaction in await transactions.GetOpenSessionsForUserAsync(userId, ct))
        {
            if (await ReconcileAsync(transaction, ct) == CheckoutReconcileOutcome.Paid) settled++;
        }
        return settled;
    }

    public async Task<int> ReconcileStaleAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var stale = await transactions.GetOpenSessionsAsync([PaymentTransactionStatus.Pending], now - PendingAfter, SweepBatch, ct);
        stale.AddRange(await transactions.GetOpenSessionsAsync([PaymentTransactionStatus.Processing], now - ProcessingAfter, SweepBatch, ct));

        foreach (var transaction in stale)
        {
            var outcome = await ReconcileAsync(transaction, ct);
            if (outcome != CheckoutReconcileOutcome.StillOpen) continue;

            // Still open at the provider: date the row so the sweep comes back to it after another
            // interval rather than on every tick. Not UpdatedAt's usual meaning, but the only other
            // writer of these rows is the state machine, which stamps its own time on every move.
            transaction.UpdatedAt = now;
            await transactions.SaveChangesAsync(ct);
        }

        return stale.Count;
    }

    private async Task<CheckoutReconcileOutcome> ReconcileAsync(PaymentTransaction transaction, CancellationToken ct)
    {
        if (!Open.Contains(transaction.Status) || transaction.ProviderSessionId is not { } sessionId)
            return CheckoutReconcileOutcome.AlreadySettled;

        var reported = await paymentProvider.ReadCheckoutSessionAsync(sessionId, ct);
        if (reported is null) return CheckoutReconcileOutcome.StillOpen;

        // Processing → the provider still says unpaid: nothing new; don't spend a dispatch on it.
        if (reported.Type == PaymentWebhookEventType.CheckoutCompletedAwaitingPayment && transaction.Status == PaymentTransactionStatus.Processing)
            return CheckoutReconcileOutcome.AwaitingPayment;

        // The provider's answer is one already acted on — the same reconcile event id, already on
        // record — yet the row is still open, because the state machine refused the move (an
        // "expired" can't end a Processing payment). Dispatching again would only bounce off the
        // idempotency gate's primary key and log a database error every sweep; treat it as still
        // open so the sweep dates the row and comes back after a full interval instead.
        if (await webhookEvents.GetAsync(reported.EventId, ct) is { Status: WebhookEventStatus.Processed or WebhookEventStatus.Ignored })
            return CheckoutReconcileOutcome.StillOpen;

        logger.LogInformation("Reconciling checkout {SessionId} ({Kind} transaction {TransactionId}) from the provider: {Reported}.",
            sessionId, transaction.Kind, transaction.Id, reported.RawType);

        var result = await dispatcher.DispatchAsync(reported, Text.Sha256Hex(reported.EventId), isReplay: false, ct);
        if (result.Outcome is WebhookDispatchOutcome.Failed or WebhookDispatchOutcome.InProgress)
            return CheckoutReconcileOutcome.Failed;

        return reported.Type switch
        {
            PaymentWebhookEventType.CheckoutCompleted => CheckoutReconcileOutcome.Paid,
            PaymentWebhookEventType.CheckoutCompletedAwaitingPayment => CheckoutReconcileOutcome.AwaitingPayment,
            PaymentWebhookEventType.CheckoutExpired => CheckoutReconcileOutcome.Expired,
            _ => CheckoutReconcileOutcome.StillOpen,
        };
    }
}
