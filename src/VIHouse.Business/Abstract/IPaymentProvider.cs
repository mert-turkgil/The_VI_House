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
    IReadOnlyDictionary<string, string> Metadata);

public record CheckoutSessionResult(string SessionId, string Url);

public enum PaymentWebhookEventType
{
    Unhandled,
    CheckoutCompleted,
    CheckoutExpired,
}

public record PaymentWebhookEvent(string EventId, PaymentWebhookEventType Type, string? SessionId);
