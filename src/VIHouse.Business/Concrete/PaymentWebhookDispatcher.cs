using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Runs a verified provider event through the three payment services with the guarantees the
/// services themselves used to have to provide one by one:
///
///   1. Exactly once. The event is inserted into WebhookEvents (unique on the provider's event id)
///      and committed <em>before</em> any handler runs. A redelivery finds the row and stops. Two
///      deliveries racing each other both try the insert; one wins, the other backs off — the
///      old check-then-mark ledger let both through.
///   2. All or nothing. The handlers share one DbContext, and used to save independently: a
///      failure in the second handler left the first one's writes committed, and a failure inside
///      a handler could leave a payment marked paid with no booking behind it. Here they run in
///      one transaction; an exception rolls back everything, the row is marked Failed with the
///      reason, and the provider is told to retry.
///   3. Written down. Every event — handled, ignored, failed — is a row with type, object id,
///      attempts and last error, which is what an admin looks at when a member says they paid.
///
/// Transient database faults are left to the provider's own retry schedule (a Failed row and a
/// 5xx) rather than an in-process retry: re-running the handlers against a change tracker that
/// still holds a half-done attempt is exactly the kind of subtle double-write this class exists
/// to rule out.
/// </summary>
public class PaymentWebhookDispatcher(
    IWebhookEventRepository events,
    IUnitOfWork unitOfWork,
    IPaymentTransactionService transactions,
    IPaymentService paymentService,
    IMembershipService membershipService,
    ISeminarService seminarService,
    IOptions<StripeOptions> stripeOptions,
    ILogger<PaymentWebhookDispatcher> logger) : IPaymentWebhookDispatcher
{
    /// <summary>Whether this host runs on live keys. A test-mode event delivered to a live host
    /// (a sandbox endpoint pointed at production by mistake) is recorded and ignored: it is signed
    /// and genuine, but it is not money.</summary>
    private bool ExpectLiveMode => stripeOptions.Value.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal)
                                   || stripeOptions.Value.SecretKey.StartsWith("rk_live_", StringComparison.Ordinal);

    /// <summary>A Received row older than this with no outcome belongs to an attempt that died
    /// mid-way (a crash, a killed process); it may be retried.</summary>
    private static readonly TimeSpan StaleAttempt = TimeSpan.FromMinutes(2);

    /// <summary>The types something acts on — the transaction state machine, a fulfilment
    /// handler, or both. Everything else is verified, recorded and marked Ignored: visible in the
    /// log, harmless to the books, until a later phase gives it a handler.</summary>
    private static readonly HashSet<PaymentWebhookEventType> Handled =
    [
        PaymentWebhookEventType.CheckoutCompleted,
        PaymentWebhookEventType.CheckoutCompletedAwaitingPayment,
        PaymentWebhookEventType.CheckoutExpired,
        PaymentWebhookEventType.CheckoutPaymentFailed,
        PaymentWebhookEventType.SubscriptionRenewed,
        PaymentWebhookEventType.SubscriptionPaymentFailed,
        PaymentWebhookEventType.SubscriptionCancelled,
        PaymentWebhookEventType.SubscriptionUpdated,
        PaymentWebhookEventType.InvoicePaymentActionRequired,
        PaymentWebhookEventType.ChargeSucceeded,
        PaymentWebhookEventType.ChargeRefunded,
        PaymentWebhookEventType.DisputeCreated,
        PaymentWebhookEventType.DisputeClosed,
        PaymentWebhookEventType.PaymentIntentProcessing,
        PaymentWebhookEventType.PaymentIntentRequiresAction,
        PaymentWebhookEventType.PaymentIntentFailed,
        PaymentWebhookEventType.PaymentIntentCanceled,
    ];

    public async Task<WebhookDispatchResult> DispatchAsync(PaymentWebhookEvent webhookEvent, string payloadHash, bool isReplay = false, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        // --- 1. The gate -------------------------------------------------------------------------
        var inserted = await events.TryInsertAsync(new WebhookEvent
        {
            EventId = webhookEvent.EventId,
            Type = webhookEvent.RawType,
            ObjectId = webhookEvent.ObjectId,
            LiveMode = webhookEvent.LiveMode,
            ReceivedAt = now,
            LastAttemptAt = now,
            Attempts = 1,
            PayloadHash = payloadHash,
        }, ct);

        if (!inserted)
        {
            var existing = await events.GetAsync(webhookEvent.EventId, ct);
            switch (existing?.Status)
            {
                case WebhookEventStatus.Processed or WebhookEventStatus.Ignored when !isReplay:
                    logger.LogInformation("Webhook {EventId} ({Type}) redelivered; already {Status}.", webhookEvent.EventId, webhookEvent.RawType, existing.Status);
                    return new WebhookDispatchResult(WebhookDispatchOutcome.Duplicate);

                case WebhookEventStatus.Received when existing.LastAttemptAt is { } started && now - started < StaleAttempt && !isReplay:
                    logger.LogWarning("Webhook {EventId} ({Type}) redelivered while an attempt started {Seconds:0}s ago is still running.", webhookEvent.EventId, webhookEvent.RawType, (now - started).TotalSeconds);
                    return new WebhookDispatchResult(WebhookDispatchOutcome.InProgress);
            }

            // Failed, stale, or an admin replay: claim a new attempt. Losing the claim means
            // another delivery took it a moment ago.
            var claimable = isReplay
                ? new[] { WebhookEventStatus.Failed, WebhookEventStatus.Received, WebhookEventStatus.Processed, WebhookEventStatus.Ignored }
                : new[] { WebhookEventStatus.Failed, WebhookEventStatus.Received };
            if (!await events.TryStartAttemptAsync(webhookEvent.EventId, claimable, now, ct))
                return new WebhookDispatchResult(WebhookDispatchOutcome.InProgress);
        }

        // --- 2. Nothing to do for this type — say so and stop -----------------------------------
        // Synthetic events (reconcile_…) carry no live-mode flag; only the provider's own are checked.
        var wrongMode = ExpectLiveMode && !webhookEvent.LiveMode && !webhookEvent.EventId.StartsWith("reconcile_", StringComparison.Ordinal);
        if (wrongMode)
            logger.LogWarning("Webhook {EventId} ({Type}) is a test-mode event on a live host; ignored.", webhookEvent.EventId, webhookEvent.RawType);

        if (wrongMode || !Handled.Contains(webhookEvent.Type))
        {
            await events.MarkIgnoredAsync(webhookEvent.EventId, now, ct);
            logger.LogInformation("Webhook {EventId} ({Type}) recorded, no handler ({Mapped}).", webhookEvent.EventId, webhookEvent.RawType, webhookEvent.Type);
            return new WebhookDispatchResult(WebhookDispatchOutcome.Ignored);
        }

        // --- 3. The handlers, together ----------------------------------------------------------
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            // The money first: the transaction row moves through the state machine, so by the time
            // a fulfilment handler runs, "is this paid" has one answer on one row. Then all three
            // handlers, unconditionally — each recognises only its own checkout sessions and
            // subscriptions (by its own table's provider reference) and no-ops otherwise.
            var money = await transactions.ApplyAsync(webhookEvent, ct);
            await paymentService.HandleWebhookEventAsync(webhookEvent, ct);
            await membershipService.HandleWebhookEventAsync(webhookEvent, ct);
            await seminarService.HandleWebhookEventAsync(webhookEvent, ct);

            await events.MarkProcessedAsync(webhookEvent.EventId, DateTimeOffset.UtcNow, ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation("Webhook {EventId} ({Type}, {ObjectId}) processed in {Elapsed} ms; transaction {Outcome} {From}→{To}.",
                webhookEvent.EventId, webhookEvent.RawType, webhookEvent.ObjectId, stopwatch.ElapsedMilliseconds, money.Outcome, money.From, money.To);
            return new WebhookDispatchResult(WebhookDispatchOutcome.Processed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            // The tracker still holds the failed attempt's entities; forget them before writing
            // the outcome, or the next SaveChanges would flush them as if nothing had rolled back.
            unitOfWork.ClearTracking();

            var error = $"{ex.GetType().Name}: {ex.Message}";
            await events.MarkFailedAsync(webhookEvent.EventId, error, DateTimeOffset.UtcNow, CancellationToken.None);

            logger.LogError(ex, "Webhook {EventId} ({Type}, {ObjectId}) failed after {Elapsed} ms; rolled back.",
                webhookEvent.EventId, webhookEvent.RawType, webhookEvent.ObjectId, stopwatch.ElapsedMilliseconds);
            return new WebhookDispatchResult(WebhookDispatchOutcome.Failed, error);
        }
    }
}
