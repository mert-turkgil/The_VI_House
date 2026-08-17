namespace VIHouse.Business.Abstract;

/// <summary>
/// The single place that renders + sends + logs a transactional email (brief §69-71). Callers
/// (ApplicationService, PaymentService) never touch IEmailSender/IEmailTemplateRenderer directly —
/// a send failure here is logged to EmailLog and swallowed, never thrown, since a broken SMTP
/// connection must never fail the business operation that triggered the email.
/// </summary>
public interface IEmailService
{
    Task SendAsync<TModel>(
        string templateKey, string recipientEmail, string subject, TModel model,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default);
}
