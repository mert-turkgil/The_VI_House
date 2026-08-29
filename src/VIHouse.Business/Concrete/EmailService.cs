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
        string templateKey, string recipientEmail, string subject, TModel model,
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

        try
        {
            var html = await renderer.RenderAsync(templateKey, model, ct);
            await sender.SendAsync(recipientEmail, subject, html, ct);
            log.Status = EmailStatus.Sent;
            log.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            // A broken SMTP connection must never fail the business operation that triggered this
            // email (approving an application, confirming a payment) — log and move on.
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = ex.Message;
            logger.LogError(ex, "Failed to send email {TemplateKey} to {Recipient}", templateKey, recipientEmail);
        }

        await emailLogs.SaveChangesAsync(ct);
        return log.Status == EmailStatus.Sent;
    }
}
