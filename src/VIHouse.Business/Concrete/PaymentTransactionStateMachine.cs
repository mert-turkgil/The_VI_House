using VIHouse.Business.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.Business.Concrete;

/// <summary>
/// The internal payment state model, in code. Two tables: which provider signal asks for which
/// state, and which moves are legal. Everything a member sees, an admin counts or a fulfilment
/// depends on derives from a transaction that went through here — Stripe's own statuses never
/// leak past this class. The same tables are written up in docs/PAYMENT_ARCHITECTURE.md.
///
/// Rules the tables encode:
///   - Succeeded is reached only from a provider signal that the money settled
///     (checkout paid, async payment succeeded, invoice paid) — never from a redirect.
///   - Processing and RequiresAction never fulfil; they exist so a delayed or challenged payment
///     is shown as such rather than as paid or as nothing.
///   - Failed is not terminal: the same invoice can be retried by the provider and succeed.
///   - Refunds only follow Succeeded (or a partial refund). Canceled/Expired/Refunded are terminal.
///   - Late or repeated events are harmless: a move the row has already made is "already
///     applied"; a move that is not legal from where the row is, is refused and logged.
/// </summary>
public static class PaymentTransactionStateMachine
{
    private static readonly IReadOnlyDictionary<PaymentTransactionStatus, PaymentTransactionStatus[]> Allowed =
        new Dictionary<PaymentTransactionStatus, PaymentTransactionStatus[]>
        {
            [PaymentTransactionStatus.Pending] =
                [PaymentTransactionStatus.Processing, PaymentTransactionStatus.RequiresAction, PaymentTransactionStatus.Succeeded,
                 PaymentTransactionStatus.Failed, PaymentTransactionStatus.Canceled, PaymentTransactionStatus.Expired],
            [PaymentTransactionStatus.Processing] =
                [PaymentTransactionStatus.Succeeded, PaymentTransactionStatus.Failed, PaymentTransactionStatus.RequiresAction, PaymentTransactionStatus.Canceled],
            [PaymentTransactionStatus.RequiresAction] =
                [PaymentTransactionStatus.Processing, PaymentTransactionStatus.Succeeded, PaymentTransactionStatus.Failed,
                 PaymentTransactionStatus.Canceled, PaymentTransactionStatus.Expired],
            [PaymentTransactionStatus.Failed] =
                [PaymentTransactionStatus.Processing, PaymentTransactionStatus.RequiresAction, PaymentTransactionStatus.Succeeded, PaymentTransactionStatus.Canceled],
            [PaymentTransactionStatus.Succeeded] =
                [PaymentTransactionStatus.PartiallyRefunded, PaymentTransactionStatus.Refunded],
            [PaymentTransactionStatus.PartiallyRefunded] =
                [PaymentTransactionStatus.PartiallyRefunded, PaymentTransactionStatus.Refunded],
            [PaymentTransactionStatus.Canceled] = [],
            [PaymentTransactionStatus.Expired] = [],
            [PaymentTransactionStatus.Refunded] = [],
        };

    public static bool CanMove(PaymentTransactionStatus from, PaymentTransactionStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>The states a move to <paramref name="to"/> is legal from — what the atomic UPDATE is guarded on.</summary>
    public static PaymentTransactionStatus[] SourcesOf(PaymentTransactionStatus to) =>
        Allowed.Where(kv => kv.Value.Contains(to)).Select(kv => kv.Key).ToArray();

    /// <summary>
    /// Provider signal → internal state. Null for events that are not about a payment's state.
    /// The refund split (full vs partial) needs the amounts, so it takes the whole event.
    /// </summary>
    public static PaymentTransactionStatus? TargetFor(PaymentWebhookEvent e) => e.Type switch
    {
        PaymentWebhookEventType.CheckoutCompleted => PaymentTransactionStatus.Succeeded,
        PaymentWebhookEventType.CheckoutCompletedAwaitingPayment => PaymentTransactionStatus.Processing,
        PaymentWebhookEventType.CheckoutPaymentFailed => PaymentTransactionStatus.Failed,
        PaymentWebhookEventType.CheckoutExpired => PaymentTransactionStatus.Expired,
        PaymentWebhookEventType.SubscriptionRenewed => PaymentTransactionStatus.Succeeded,
        PaymentWebhookEventType.SubscriptionPaymentFailed => PaymentTransactionStatus.Failed,
        PaymentWebhookEventType.InvoicePaymentActionRequired => PaymentTransactionStatus.RequiresAction,
        PaymentWebhookEventType.PaymentIntentProcessing => PaymentTransactionStatus.Processing,
        PaymentWebhookEventType.PaymentIntentRequiresAction => PaymentTransactionStatus.RequiresAction,
        PaymentWebhookEventType.PaymentIntentFailed => PaymentTransactionStatus.Failed,
        PaymentWebhookEventType.PaymentIntentCanceled => PaymentTransactionStatus.Canceled,
        PaymentWebhookEventType.ChargeRefunded =>
            e.AmountRefundedMinor is { } refunded && e.AmountMinor is { } total && refunded >= total
                ? PaymentTransactionStatus.Refunded
                : PaymentTransactionStatus.PartiallyRefunded,
        // Disputes flag the row; subscription lifecycle events move the membership, not the money.
        _ => null,
    };
}
