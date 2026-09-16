using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Communication;

namespace VIHouse.WebUI.Services;

/// <summary>
/// The Identity UI's IEmailSender, backed by the site's SMTP sender and logged like everything else.
///
/// Without a registration, AddDefaultUI supplies a sender that does nothing — and for a long time
/// that is exactly what "forgot password" did here: no error, no log row, no email. Every Identity
/// page now sends through IEmailService with a real template, so this adapter should never be
/// called; it exists so that if a scaffolded page ever reaches for the framework's sender again,
/// the message still leaves the building and still shows up in Admin > Emails, under a key that
/// makes the gap obvious.
/// </summary>
public sealed class IdentityEmailSenderAdapter(IEmailSender sender, IEmailLogRepository emailLogs, ILogger<IdentityEmailSenderAdapter> logger)
    : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var log = new EmailLog
        {
            TemplateKey = "Identity.Untemplated",
            RecipientEmail = email,
            Subject = subject,
            Status = EmailStatus.Queued,
        };
        await emailLogs.AddAsync(log);
        await emailLogs.SaveChangesAsync();

        try
        {
            await sender.SendAsync(email, subject, htmlMessage);
            log.Status = EmailStatus.Sent;
            log.SentAt = DateTimeOffset.UtcNow;
            logger.LogWarning("An Identity page sent an untemplated email ({Subject}) to {Email} — give it a template under Views/Emails.", subject, email);
        }
        catch (Exception ex)
        {
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = ex.Message;
            logger.LogError(ex, "Failed to send Identity email {Subject} to {Email}", subject, email);
        }

        await emailLogs.SaveChangesAsync();
    }
}
