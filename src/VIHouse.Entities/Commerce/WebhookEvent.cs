namespace VIHouse.Entities.Commerce;

/// <summary>
/// One row per inbound payment-provider webhook delivery, keyed on the provider's own event id
/// (Stripe's evt_…). This is both the idempotency gate — the row is inserted, under a unique key,
/// before any handler runs, so a second delivery of the same event cannot get past it — and the
/// operational record that answers "did the event arrive, what did we do with it, and if it
/// failed, why". Replaces the older ProcessedWebhookEvents ledger (brief §32), whose rows were
/// migrated in as <see cref="WebhookEventStatus.Processed"/>.
///
/// The payload itself is not stored: it carries the buyer's details and is retrievable from the
/// provider by event id for thirty days, which is also how an admin re-runs a failed event.
/// </summary>
public class WebhookEvent
{
    /// <summary>The provider's event id — the primary key, hence unique.</summary>
    public string EventId { get; set; } = default!;

    /// <summary>The provider's raw event type, e.g. "checkout.session.completed".</summary>
    public string Type { get; set; } = default!;

    /// <summary>The id of the provider object the event is about — a session, invoice,
    /// subscription or charge id — for correlating with our own rows.</summary>
    public string? ObjectId { get; set; }

    public bool LiveMode { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Received;

    /// <summary>How many times a handler run was started for this event — the provider's own
    /// retries and admin re-runs included.</summary>
    public int Attempts { get; set; }

    /// <summary>The exception message of the last failed attempt; cleared on success.</summary>
    public string? LastError { get; set; }

    /// <summary>SHA-256 of the raw body, so two deliveries with the same id but different content
    /// (which should never happen) can be told apart without keeping the content.</summary>
    public string PayloadHash { get; set; } = default!;
}

public enum WebhookEventStatus
{
    /// <summary>Inserted; a handler run is in progress (or was interrupted — see LastAttemptAt).</summary>
    Received,
    /// <summary>Every handler ran and the transaction committed.</summary>
    Processed,
    /// <summary>A handler threw; nothing was committed; the provider was told to retry.</summary>
    Failed,
    /// <summary>Verified and recorded, but no handler acts on this event type.</summary>
    Ignored,
}
