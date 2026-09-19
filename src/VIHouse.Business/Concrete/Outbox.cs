using System.Text.Json;
using Microsoft.Extensions.Logging;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Notifications;

namespace VIHouse.Business.Concrete;

public class Outbox(IOutboxRepository messages, ILogger<Outbox> logger) : IOutbox
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task EnqueueEmailAsync<TModel>(string dedupeKey, string templateKey, string recipientEmail, string subject, TModel model,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default) where TModel : notnull =>
        EnqueueAsync(OutboxMessageKind.Email, dedupeKey,
            new OutboxEmailPayload(templateKey, recipientEmail, subject, typeof(TModel).AssemblyQualifiedName!, JsonSerializer.Serialize(model, Json)),
            relatedEntityType, relatedEntityId, ct);

    public Task EnqueueSmsAsync(string dedupeKey, string templateKey, string? recipientPhone, string body,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default) =>
        EnqueueAsync(OutboxMessageKind.Sms, dedupeKey, new OutboxSmsPayload(templateKey, recipientPhone, body), relatedEntityType, relatedEntityId, ct);

    public Task EnqueueNotificationAsync(string dedupeKey, Guid userId, NotificationType type, string title, string body, string? link = null,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default) =>
        EnqueueAsync(OutboxMessageKind.Notification, dedupeKey, new OutboxNotificationPayload(userId, type, title, body, link), relatedEntityType, relatedEntityId, ct);

    private async Task EnqueueAsync<TPayload>(OutboxMessageKind kind, string dedupeKey, TPayload payload, string? relatedEntityType, Guid? relatedEntityId, CancellationToken ct)
    {
        var key = dedupeKey.Length <= 300 ? dedupeKey : dedupeKey[..300];

        // The read is the fast path; the unique index is the guarantee. Two handlers racing on the
        // same key (a reconcile and the webhook) both pass the read, one loses the insert — and
        // since that insert happens in the loser's transaction, the loser's whole attempt rolls
        // back and is retried against a row that has already moved on, where it enqueues nothing.
        if (await messages.ExistsAsync(key, ct))
        {
            logger.LogDebug("Outbox: {Key} already queued; skipped.", key);
            return;
        }

        await messages.AddAsync(new OutboxMessage
        {
            Kind = kind,
            DedupeKey = key,
            Payload = JsonSerializer.Serialize(payload, Json),
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
        }, ct);
        await messages.SaveChangesAsync(ct);
    }
}
