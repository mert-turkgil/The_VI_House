namespace VIHouse.Entities.Commerce;

/// <summary>
/// Idempotency ledger for inbound payment-provider webhooks (brief §32). EventId is the
/// provider's own event ID (e.g. Stripe's evt_...) and is the primary key — a webhook handler
/// checks for an existing row before doing any work, so a duplicate delivery is a safe no-op.
/// </summary>
public class ProcessedWebhookEvent
{
    public string EventId { get; set; } = default!;
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
