using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

/// <summary>
/// A side effect (an email, a text, an in-app notification) that a payment handler decided to
/// cause, written in the <em>same database transaction</em> as the state change that caused it,
/// and delivered afterwards by the outbox processor.
///
/// Two things this buys, both of which the old "send from inside the handler" could not:
///   - Nothing is sent for work that rolled back. A handler that emailed a confirmation and then
///     failed used to leave the member with an email for a booking that did not exist, and a
///     second email when the provider retried.
///   - Nothing is sent twice. <see cref="DedupeKey"/> is unique; a handler that runs again for
///     the same outcome (a redelivery, a reconcile that races the webhook) enqueues the same key
///     and the second row is simply not inserted.
/// The provider's webhook is answered as soon as the transaction commits, without waiting on an
/// SMTP relay — the "return 2xx quickly" the provider asks for.
/// </summary>
public class OutboxMessage : BaseEntity
{
    public OutboxMessageKind Kind { get; set; }

    /// <summary>What makes this effect one-of-a-kind: e.g. "email:BookingConfirmed:Booking:{id}".
    /// The same effect for the same outcome always builds the same key.</summary>
    public string DedupeKey { get; set; } = default!;

    /// <summary>The effect's arguments as JSON — see OutboxEmailPayload / OutboxSmsPayload /
    /// OutboxNotificationPayload in the Business layer.</summary>
    public string Payload { get; set; } = default!;

    /// <summary>What the effect is about, for the log screens and for support.</summary>
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }

    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}

public enum OutboxMessageKind
{
    Email,
    Sms,
    Notification,
}
