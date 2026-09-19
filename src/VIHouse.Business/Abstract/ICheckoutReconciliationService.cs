namespace VIHouse.Business.Abstract;

/// <summary>
/// The provider's own guidance for Checkout: fulfil from the webhook <em>and</em> from the landing
/// page, through one idempotent function that reads the session's payment status from the API.
/// This is that function. Every caller — the success pages, a member's account page, the
/// background sweep — asks the provider what a session's state is and feeds the answer through
/// <see cref="IPaymentWebhookDispatcher"/> exactly as a webhook would be, so the event log, the
/// transaction state machine and the fulfilment handlers see one path.
///
/// What it never does: trust the browser. The session id in a success URL only says which row to
/// ask the provider about; the provider's answer, read with the secret key, is the only input.
/// </summary>
public interface ICheckoutReconciliationService
{
    /// <summary>Asks the provider about one session we opened. Sessions we have no transaction for
    /// are not looked up at all — an unknown id in a URL costs nothing and learns nothing.</summary>
    Task<CheckoutReconcileOutcome> ReconcileSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Every open checkout of one account. Returns how many the provider reported settled.</summary>
    Task<int> ReconcileForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The sweep: open checkouts nobody has looked at for a while — a webhook that never
    /// arrived, a delayed payment that settled days later. Returns how many were read.</summary>
    Task<int> ReconcileStaleAsync(CancellationToken ct = default);
}

public enum CheckoutReconcileOutcome
{
    /// <summary>No transaction for that session, or the id is not a session id.</summary>
    Unknown,
    /// <summary>The transaction is already in a final state; nothing was read.</summary>
    AlreadySettled,
    /// <summary>The provider still has the session open, or could not be reached.</summary>
    StillOpen,
    /// <summary>The provider reported the session complete and paid; the outcome has been applied (or already was).</summary>
    Paid,
    /// <summary>The provider reported the checkout complete but the money not yet arrived.</summary>
    AwaitingPayment,
    /// <summary>The provider reported the session expired unpaid.</summary>
    Expired,
    /// <summary>The provider's answer could not be applied; it will be retried.</summary>
    Failed,
}
