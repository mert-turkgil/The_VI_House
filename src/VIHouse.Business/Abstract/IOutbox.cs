using VIHouse.Entities.Notifications;

namespace VIHouse.Business.Abstract;

/// <summary>
/// Where a payment handler puts the things it wants to happen <em>because</em> of a state change
/// — the confirmation email, the text, the in-app line — instead of doing them on the spot. Rows
/// go into OutboxMessages inside the caller's database transaction; the processor delivers them
/// after it commits. See OutboxMessage for the two guarantees this gives.
///
/// Every method takes a dedupe key. Build it from the outcome, not the event: two events that
/// mean the same thing for the same row (a redelivery, a reconcile racing the webhook) must build
/// the same key, so the second is a no-op.
/// </summary>
public interface IOutbox
{
    /// <param name="culture">One of SiteCultures.Names — required, same rule as IEmailService.SendAsync:
    /// the recipient's known PreferredCulture (falling back to SiteCultures.Default), or
    /// SiteCultures.Default for an internal staff alert. Captured now because the enqueuing
    /// request's own ambient culture (the actor's, not necessarily the recipient's) will be long
    /// gone by the time OutboxProcessor actually sends this.</param>
    Task EnqueueEmailAsync<TModel>(string dedupeKey, string templateKey, string recipientEmail, string subject, TModel model, string culture,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default) where TModel : notnull;

    Task EnqueueSmsAsync(string dedupeKey, string templateKey, string? recipientPhone, string body,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default);

    Task EnqueueNotificationAsync(string dedupeKey, Guid userId, NotificationType type, string title, string body, string? link = null,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default);
}

/// <summary>Delivers what <see cref="IOutbox"/> queued. Driven by a hosted service.</summary>
public interface IOutboxProcessor
{
    /// <summary>Delivers up to <paramref name="batchSize"/> due messages. Returns how many were attempted.</summary>
    Task<int> ProcessDueAsync(int batchSize, CancellationToken ct = default);
}

public sealed record OutboxEmailPayload(string TemplateKey, string RecipientEmail, string Subject, string ModelType, string ModelJson, string Culture);
public sealed record OutboxSmsPayload(string TemplateKey, string? RecipientPhone, string Body);
public sealed record OutboxNotificationPayload(Guid UserId, NotificationType Type, string Title, string Body, string? Link);
