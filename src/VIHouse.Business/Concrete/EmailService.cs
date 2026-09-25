using Microsoft.Extensions.Logging;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Communication;

namespace VIHouse.Business.Concrete;

public class EmailService(
    IEmailTemplateRenderer renderer,
    IEmailSender sender,
    IEmailLogRepository emailLogs,
    ILogger<EmailService> logger) : IEmailService
{
    public async Task<bool> SendAsync<TModel>(
        string templateKey, string recipientEmail, string subject, TModel model, string culture,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default)
    {
        var log = new EmailLog
        {
            TemplateKey = templateKey,
            RecipientEmail = recipientEmail,
            Subject = subject,
            Status = EmailStatus.Queued,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
        };
        await emailLogs.AddAsync(log, ct);
        await emailLogs.SaveChangesAsync(ct); // persist the Queued row first — a send failure below must still leave an audit trail

        string? html = null;
        try
        {
            var rendered = await renderer.RenderAsync(templateKey, model, culture, ct);
            html = rendered.Html;
            log.Subject = rendered.Subject ?? subject;
            await sender.SendAsync(recipientEmail, log.Subject, html, ct);
            log.Status = EmailStatus.Sent;
            log.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            // A broken SMTP connection must never fail the business operation that triggered this
            // email (approving an application, confirming a payment) — log and move on. The rendered
            // body is kept (when rendering got that far) so the mail can be resent from the admin log.
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = ex.Message;
            log.Body = html;
            logger.LogError(ex, "Failed to send email {TemplateKey} to {Recipient}", templateKey, recipientEmail);
        }

        await emailLogs.SaveChangesAsync(ct);
        return log.Status == EmailStatus.Sent;
    }

    public async Task<ResendResult> ResendAsync(Guid logId, CancellationToken ct = default)
    {
        var failed = await emailLogs.GetByIdAsync(logId, ct);
        if (failed is null) return ResendResult.Refused("That email is no longer in the log.");
        if (failed.Status != EmailStatus.Failed) return ResendResult.Refused("Only a failed email can be resent.");
        if (failed.ResentAt is not null) return ResendResult.Refused("That email has already been resent — see the newer row.");
        if (string.IsNullOrEmpty(failed.Body))
            return ResendResult.Refused("There is no stored copy of this email to resend (it failed before it was rendered, or predates resending).");

        var body = failed.Body;

        // A new attempt is a new row: the failure stays on record, and this one says what happened next.
        var attempt = new EmailLog
        {
            TemplateKey = failed.TemplateKey,
            RecipientEmail = failed.RecipientEmail,
            Subject = failed.Subject,
            Status = EmailStatus.Queued,
            RelatedEntityType = failed.RelatedEntityType,
            RelatedEntityId = failed.RelatedEntityId,
        };
        failed.ResentAt = DateTimeOffset.UtcNow;
        failed.Body = null;
        await emailLogs.AddAsync(attempt, ct);
        await emailLogs.SaveChangesAsync(ct);

        try
        {
            await sender.SendAsync(attempt.RecipientEmail, attempt.Subject, body, ct);
            attempt.Status = EmailStatus.Sent;
            attempt.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            attempt.Status = EmailStatus.Failed;
            attempt.ErrorMessage = ex.Message;
            attempt.Body = body; // still owed — the new row carries it for the next try
            logger.LogError(ex, "Resend of email {LogId} to {Recipient} failed", logId, attempt.RecipientEmail);
        }

        await emailLogs.SaveChangesAsync(ct);
        return attempt.Status == EmailStatus.Sent
            ? ResendResult.Ok($"Resent to {attempt.RecipientEmail}.")
            : ResendResult.Refused($"Resending to {attempt.RecipientEmail} failed again: {attempt.ErrorMessage}");
    }
}
