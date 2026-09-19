namespace VIHouse.Business.Abstract;

/// <summary>
/// The one path every verified payment-provider event takes: record it under its unique id,
/// run every handler inside a single database transaction, and write down what happened. See
/// PaymentWebhookDispatcher for the guarantees.
/// </summary>
public interface IPaymentWebhookDispatcher
{
    /// <param name="payloadHash">SHA-256 of the raw request body, hex — kept instead of the body.</param>
    /// <param name="isReplay">True when an admin re-runs an event that already has a row.</param>
    Task<WebhookDispatchResult> DispatchAsync(PaymentWebhookEvent webhookEvent, string payloadHash, bool isReplay = false, CancellationToken ct = default);
}

public enum WebhookDispatchOutcome
{
    /// <summary>Every handler ran; the transaction committed.</summary>
    Processed,
    /// <summary>Recorded; no handler acts on this event type.</summary>
    Ignored,
    /// <summary>This event id was already processed or ignored — a redelivery. Nothing ran.</summary>
    Duplicate,
    /// <summary>Another delivery of this event is being processed right now. The caller should ask
    /// the provider to retry later rather than run it twice.</summary>
    InProgress,
    /// <summary>A handler threw. Nothing was committed; the row says why. The provider should retry.</summary>
    Failed,
}

public record WebhookDispatchResult(WebhookDispatchOutcome Outcome, string? Error = null);
