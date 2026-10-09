using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Communication;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Turns outbox rows back into the calls the handler would have made. Each row is attempted on
/// its own; a failure records the error and pushes the next attempt out (1, 2, 4 … minutes, up
/// to <see cref="MaxAttempts"/>), never blocking the rows behind it. The email service swallows
/// transport failures into its own log and returns false; that false is treated as a failure here
/// too, so an SMTP outage (a wrong password, a server down for an hour) is retried on the backoff
/// instead of the message being marked delivered and quietly lost.
/// </summary>
public class OutboxProcessor(
    IOutboxRepository messages,
    IEmailService emailService,
    IEmailLogRepository emailLogs,
    ISmsService smsService,
    INotificationService notificationService,
    ILogger<OutboxProcessor> logger) : IOutboxProcessor
{
    public const int MaxAttempts = 8;

    private static readonly MethodInfo SendEmail = typeof(IEmailService).GetMethod(nameof(IEmailService.SendAsync))!;

    public async Task<int> ProcessDueAsync(int batchSize, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await messages.GetDueAsync(now, batchSize, MaxAttempts, ct);
        if (due.Count == 0) return 0;

        foreach (var message in due)
        {
            // Claimed before delivery: a second processor (two hosts, an overlapping tick) that
            // reads the same row a moment later sees it already dated past "now".
            message.Attempts++;
            message.NextAttemptAt = now.AddMinutes(Math.Pow(2, message.Attempts - 1));
            message.UpdatedAt = now;
            await messages.SaveChangesAsync(ct);

            try
            {
                await DeliverAsync(message, ct);
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
                // Delivered: the arguments (a password-setup link, a phone number) have done their
                // job and are not kept. The email/SMS logs are the record of what went out.
                message.Payload = "{}";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.LastError = Text.Clip($"{ex.GetType().Name}: {ex.Message}", 2000);
                if (ex is EmailNotSentException notSent && message.Attempts < MaxAttempts)
                    await SupersedeFailedLogAsync(notSent, now, message.Attempts, ct);
                if (message.Attempts >= MaxAttempts)
                    logger.LogCritical(ex, "Outbox {Kind} {Key} gave up after {Max} attempts.", message.Kind, message.DedupeKey, MaxAttempts);
                else
                    logger.LogError(ex, "Outbox {Kind} {Key} failed (attempt {Attempt}/{Max}).", message.Kind, message.DedupeKey, message.Attempts, MaxAttempts);
            }

            await messages.SaveChangesAsync(ct);
        }

        return due.Count;
    }

    /// <summary>
    /// The failed log row this attempt left behind stays on record, but not as something to resend:
    /// the outbox will try again itself, and a manual Resend (or Retry failed) on top of that would
    /// deliver the same email twice. Only the final attempt's failure is left resendable.
    /// </summary>
    private async Task SupersedeFailedLogAsync(EmailNotSentException notSent, DateTimeOffset attemptStartedAt, int attempt, CancellationToken ct)
    {
        var rows = await emailLogs.FindAsync(e => e.Status == EmailStatus.Failed && e.ResentAt == null
            && e.RecipientEmail == notSent.RecipientEmail && e.TemplateKey == notSent.TemplateKey && e.CreatedAt >= attemptStartedAt, ct);
        foreach (var row in rows)
        {
            row.ResentAt = DateTimeOffset.UtcNow;
            row.Body = null;
            row.ErrorMessage = Text.Clip($"{RetryingPrefix} (attempt {attempt}/{MaxAttempts}): {row.ErrorMessage}", 2000);
        }
        if (rows.Count > 0) await emailLogs.SaveChangesAsync(ct);
    }

    /// <summary>How a superseded attempt's error starts — the admin log labels those rows by it.</summary>
    public const string RetryingPrefix = "Retried automatically";

    private async Task DeliverAsync(OutboxMessage message, CancellationToken ct)
    {
        switch (message.Kind)
        {
            case OutboxMessageKind.Email:
            {
                var payload = JsonSerializer.Deserialize<OutboxEmailPayload>(message.Payload, Outbox.Json)
                    ?? throw new InvalidOperationException("Empty email payload.");
                var modelType = Type.GetType(payload.ModelType)
                    ?? throw new InvalidOperationException($"Unknown email model type {payload.ModelType}.");
                var model = JsonSerializer.Deserialize(payload.ModelJson, modelType, Outbox.Json)
                    ?? throw new InvalidOperationException("Email model did not deserialise.");

                var task = (Task<bool>)SendEmail.MakeGenericMethod(modelType).Invoke(emailService,
                    [payload.TemplateKey, payload.RecipientEmail, payload.Subject, model, payload.Culture, message.RelatedEntityType, message.RelatedEntityId, ct])!;
                if (!await task)
                    throw new EmailNotSentException(payload.TemplateKey, payload.RecipientEmail);
                break;
            }
            case OutboxMessageKind.Sms:
            {
                var payload = JsonSerializer.Deserialize<OutboxSmsPayload>(message.Payload, Outbox.Json)
                    ?? throw new InvalidOperationException("Empty SMS payload.");
                await smsService.SendAsync(payload.TemplateKey, payload.RecipientPhone, payload.Body, message.RelatedEntityType, message.RelatedEntityId, ct);
                break;
            }
            case OutboxMessageKind.Notification:
            {
                var payload = JsonSerializer.Deserialize<OutboxNotificationPayload>(message.Payload, Outbox.Json)
                    ?? throw new InvalidOperationException("Empty notification payload.");
                await notificationService.CreateForUserAsync(payload.UserId, payload.Type, payload.Title, payload.Body, payload.Link, ct);
                break;
            }
            default:
                throw new InvalidOperationException($"Unknown outbox kind {message.Kind}.");
        }
    }
}

/// <summary>The email service reported a send failure (already logged in EmailLogs with the reason).</summary>
public sealed class EmailNotSentException(string templateKey, string recipientEmail)
    : Exception($"{templateKey} to {recipientEmail} was not sent — see Admin › Emails for the server's reason. Will retry.")
{
    public string TemplateKey { get; } = templateKey;
    public string RecipientEmail { get; } = recipientEmail;
}
