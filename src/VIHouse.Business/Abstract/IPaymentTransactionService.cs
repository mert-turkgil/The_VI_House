using VIHouse.Entities.Commerce;

namespace VIHouse.Business.Abstract;

/// <summary>
/// Owns the PaymentTransaction rows: opens one when a checkout starts, attaches the provider's
/// ids as they become known, and — the part that matters — moves it between internal states
/// only in response to provider events, through PaymentTransactionStateMachine. Nothing else
/// writes a transaction's status.
/// </summary>
public interface IPaymentTransactionService
{
    /// <summary>A new Pending transaction for a checkout about to be opened at the provider.</summary>
    Task<PaymentTransaction> OpenAsync(PaymentTransactionKind kind, Guid? userId, string relatedEntityType, Guid relatedEntityId,
        long amountMinor, string currency, CancellationToken ct = default);

    /// <summary>The provider handed back a session id for this transaction.</summary>
    Task AttachSessionAsync(Guid transactionId, string providerSessionId, string? providerCustomerId, CancellationToken ct = default);

    /// <summary>The checkout could not be opened at the provider (or was superseded by a newer
    /// one): Pending → Canceled with the reason. A no-op if the row already moved on.</summary>
    Task CancelOpenAsync(Guid transactionId, string reason, CancellationToken ct = default);

    /// <summary>An account now exists for a transaction that was opened before one did (the /join
    /// flow). Also records the provider's customer id on the account, once, so later checkouts
    /// reuse it.</summary>
    Task AssignUserAsync(Guid transactionId, Guid userId, string? providerCustomerId, CancellationToken ct = default);

    /// <summary>
    /// Applies one provider event to the transaction it concerns. The single place a provider
    /// status becomes an internal one. Safe to call for every event: an event that concerns no
    /// transaction, or asks for a move the state machine forbids, changes nothing and says so.
    /// </summary>
    Task<PaymentTransactionEventResult> ApplyAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default);

    Task<PaymentTransaction?> GetBySessionAsync(string providerSessionId, CancellationToken ct = default);
    Task<PaymentTransaction?> GetByInvoiceAsync(string providerInvoiceId, CancellationToken ct = default);

    /// <summary>How a refund or dispute event finds the purchase it is about: the intent is learned
    /// when the checkout completes and is the only id those events carry.</summary>
    Task<PaymentTransaction?> GetByPaymentIntentAsync(string providerPaymentIntentId, CancellationToken ct = default);

    /// <summary>The newest still-Pending transaction for a fulfilment row, if any — the one a
    /// fresh checkout attempt supersedes.</summary>
    Task<PaymentTransaction?> GetLatestOpenForAsync(string relatedEntityType, Guid relatedEntityId, CancellationToken ct = default);
}

public enum PaymentTransactionEventOutcome
{
    /// <summary>The event is not about a payment's state (a subscription update, an unknown type).</summary>
    NotApplicable,
    /// <summary>No transaction matches the provider ids on the event — a checkout opened before
    /// transactions existed, or one the provider knows and we do not.</summary>
    NoTransaction,
    /// <summary>The row was already past this state (a redelivery, or an event arriving late).</summary>
    AlreadyApplied,
    /// <summary>The move the event asks for is not legal from the row's current state; recorded, not applied.</summary>
    IllegalTransition,
    Applied,
}

public record PaymentTransactionEventResult(
    PaymentTransactionEventOutcome Outcome,
    PaymentTransaction? Transaction,
    PaymentTransactionStatus? From,
    PaymentTransactionStatus? To);
