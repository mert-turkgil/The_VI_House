namespace VIHouse.Business.Abstract;

/// <summary>The actual transport (SMTP via MailKit — see Concrete/SmtpEmailSender). Throws on failure; IEmailService is what decides that's non-fatal to the caller.</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}
