namespace VIHouse.Business.Abstract;

/// <summary>
/// Provider-agnostic boundary (brief §29/30) — Stripe.net types never cross this interface, so
/// PaymentService and everything above it stays provider-independent. Stripe is the only Phase 1
/// implementation (see Concrete/StripePaymentProvider), but swapping/adding a provider later means
/// writing one new class against this contract, not touching PaymentService.
/// </summary>
public interface IPaymentProvider
{
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken ct = default);

    /// <summary>Verifies the inbound webhook's signature and maps it to a provider-agnostic result. Throws if the signature is invalid.</summary>
    PaymentWebhookEvent ConstructWebhookEvent(string requestBody, string signatureHeader);

    /// <summary>Live read of a payment's current state directly from the provider — for admin
    /// screens that want real card/receipt/refund details beyond what's stored locally. Returns
    /// null (never throws) if the provider can't produce details for any reason — not-found
    /// reference, network failure, or a reference that was never a real provider session (see
    /// PaymentService.InitiateCheckoutAsync's placeholder ProviderReference). Callers fall back to
    /// local DB fields when this is null.</summary>
    Task<PaymentProviderDetails?> GetPaymentDetailsAsync(string providerReference, CancellationToken ct = default);
}

public record CreateCheckoutSessionRequest(
    string CustomerEmail,
    string ProductName,
    string? ProductDescription,
    long AmountMinor,
    string Currency,
    string SuccessUrl,
    string CancelUrl,
    string ClientReferenceId,
    IReadOnlyDictionary<string, string> Metadata)
{
    /// <summary>
    /// Null for a one-off charge. Set it to bill the customer on a repeating schedule instead — the
    /// provider then collects a payment method it can reuse, and keeps charging until the
    /// subscription is cancelled. Deliberately expressed as an interval rather than a provider
    /// price id so nothing above this interface has to know Stripe exists.
    /// </summary>
    public RecurringInterval? Recurring { get; init; }
}

public enum RecurringInterval
{
    Monthly,
    Annual,
}

public record CheckoutSessionResult(string SessionId, string Url);

public record PaymentProviderDetails(
    string? SessionStatus,
    string? PaymentStatus,
    string? PaymentIntentStatus,
    string? ChargeStatus,
    long? AmountCapturedMinor,
    string? CardBrand,
    string? CardLast4,
    string? ReceiptUrl,
    bool? Refunded,
    long? AmountRefundedMinor,
    bool? Disputed);

public enum PaymentWebhookEventType
{
    Unhandled,
    CheckoutCompleted,
    CheckoutExpired,
}

public record PaymentWebhookEvent(string EventId, PaymentWebhookEventType Type, string? SessionId);
