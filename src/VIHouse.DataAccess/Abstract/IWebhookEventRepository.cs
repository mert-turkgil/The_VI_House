using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

/// <summary>
/// The inbound webhook log (brief §32). The status updates are deliberately direct SQL updates
/// rather than tracked changes: the dispatcher calls them after a rolled-back transaction, when
/// the shared change tracker still holds the failed attempt's entities, and a tracked save would
/// try to flush those too.
/// </summary>
public interface IWebhookEventRepository
{
    Task<WebhookEvent?> GetAsync(string eventId, CancellationToken ct = default);

    /// <summary>Inserts and commits the row on its own. Returns false — writing nothing — when a
    /// row with this event id already exists; the caller then reads it to decide what to do.</summary>
    Task<bool> TryInsertAsync(WebhookEvent webhookEvent, CancellationToken ct = default);

    /// <summary>Claims a fresh attempt: increments Attempts, stamps LastAttemptAt, sets Received.
    /// Returns false when the row is not in one of the given statuses (someone else got there first).</summary>
    Task<bool> TryStartAttemptAsync(string eventId, IReadOnlyCollection<WebhookEventStatus> from, DateTimeOffset now, CancellationToken ct = default);

    Task MarkProcessedAsync(string eventId, DateTimeOffset now, CancellationToken ct = default);
    Task MarkIgnoredAsync(string eventId, DateTimeOffset now, CancellationToken ct = default);
    Task MarkFailedAsync(string eventId, string error, DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Newest first, for the admin screen.</summary>
    Task<List<WebhookEvent>> ListAsync(WebhookEventStatus? status, int take, CancellationToken ct = default);
    Task<Dictionary<WebhookEventStatus, int>> CountByStatusAsync(CancellationToken ct = default);

    /// <summary>Every event recorded against any of the given provider object ids (a session, an
    /// invoice, a payment intent, a subscription), newest first — one transaction's timeline.</summary>
    Task<List<WebhookEvent>> ListByObjectIdsAsync(IReadOnlyCollection<string> objectIds, CancellationToken ct = default);
}
