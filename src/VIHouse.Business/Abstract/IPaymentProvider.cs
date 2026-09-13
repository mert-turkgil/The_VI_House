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

    /// <summary>
    /// A one-time URL to the provider's hosted self-service billing page, where the member can
    /// change card, download invoices or cancel a subscription. Returns null (never throws) when
    /// the provider cannot open one — no such customer, portal not configured on the provider
    /// side, network failure — so a member-facing page can simply not show the button.
    /// </summary>
    Task<string?> CreateBillingPortalUrlAsync(string providerCustomerId, string returnUrl, CancellationToken ct = default);
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

    /// <summary>
    /// The provider's own Price id for what is being sold, when the plan has been mirrored into the
    /// provider's catalogue (see IPaymentCatalogProvider). When set, the checkout line references
    /// that price and the amount/currency/interval above are informational only; when null, the
    /// provider is given the amount inline exactly as before. Either way the member sees the same
    /// charge — the difference is whether the provider's dashboard groups the sale under a named
    /// product.
    /// </summary>
    public string? ProviderPriceId { get; init; }
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

    /// <summary>A recurring subscription was billed again for a new period — not the first
    /// payment, which arrives as <see cref="CheckoutCompleted"/>.</summary>
    SubscriptionRenewed,

    /// <summary>The subscription has ended at the provider — cancelled by the member through the
    /// billing portal, by an admin in the provider's dashboard, or by repeated payment failure.</summary>
    SubscriptionCancelled,
}

/// <param name="SessionId">The checkout session, for the two Checkout* events.</param>
public record PaymentWebhookEvent(string EventId, PaymentWebhookEventType Type, string? SessionId)
{
    /// <summary>The provider's subscription id. Set on a completed subscription checkout and on
    /// both Subscription* events — it is the key membership rows are matched on.</summary>
    public string? SubscriptionId { get; init; }

    /// <summary>The provider's customer id, when the event carries one.</summary>
    public string? CustomerId { get; init; }

    /// <summary>For <see cref="PaymentWebhookEventType.SubscriptionRenewed"/>: when the period
    /// just paid for ends, i.e. the new expiry.</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; init; }
}
