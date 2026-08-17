namespace VIHouse.Business.Abstract;

public interface IPaymentService
{
    /// <summary>Read-only landing-page data for an invitation code — validity, the experience, and its purchasable ticket types.</summary>
    Task<InvitationLandingInfo> GetInvitationLandingAsync(string invitationCode, CancellationToken ct = default);

    /// <summary>
    /// Redeems an invitation into a live Stripe Checkout Session: validates the invitation/application/
    /// ticket type, applies an optional promo code, reserves capacity (brief §24), provisions the
    /// member account if this is their first payment, and persists a local Payment(Status=Created)
    /// row before ever redirecting the browser (brief §32 — the redirect itself is never trusted).
    /// </summary>
    Task<CheckoutInitiationResult> InitiateCheckoutAsync(
        string invitationCode, Guid ticketTypeId, string? promoCode, string successUrl, string cancelUrl, CancellationToken ct = default);

    /// <summary>Processes a signature-verified webhook event. Safe to call twice for the same event (checked against ProcessedWebhookEvent first).</summary>
    Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default);

    /// <summary>
    /// Reads LOCAL state only for the success-page redirect — never trusts the browser's return from
    /// Stripe on its own (brief §32). IsConfirmed is false if the webhook hasn't landed yet; the
    /// caller should show a "processing" state and let the page refresh rather than treat that as a failure.
    /// </summary>
    Task<BookingConfirmationInfo?> GetBookingConfirmationBySessionAsync(string sessionId, CancellationToken ct = default);
}

public record CheckoutInitiationResult(bool Success, string? CheckoutUrl, string? Error)
{
    public static CheckoutInitiationResult Ok(string url) => new(true, url, null);
    public static CheckoutInitiationResult Fail(string error) => new(false, null, error);
}

public record InvitationLandingInfo(
    bool IsValid,
    string? InvalidReason,
    string? ApplicantFirstName,
    string? ExperienceTitle,
    string? ExperienceCity,
    string? ExperienceCountry,
    DateTimeOffset ExperienceStartAtUtc,
    DateTimeOffset ExperienceEndAtUtc,
    List<TicketTypeOption> TicketTypes)
{
    public static InvitationLandingInfo Invalid(string reason) =>
        new(false, reason, null, null, null, null, default, default, []);
}

public record TicketTypeOption(Guid Id, string Title, string? Description, long PriceMinor, string Currency, string? PerksText, bool IsSoldOut);

public record BookingConfirmationInfo(
    bool IsConfirmed,
    string? BookingReference,
    string? ExperienceTitle,
    string? ExperienceCity,
    long AmountMinor,
    string Currency,
    Guid? UserId);
