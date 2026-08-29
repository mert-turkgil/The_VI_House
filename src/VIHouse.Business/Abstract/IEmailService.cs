namespace VIHouse.Business.Abstract;

/// <summary>
/// The single place that renders + sends + logs a transactional email (brief §69-71). Callers
/// (ApplicationService, PaymentService) never touch IEmailSender/IEmailTemplateRenderer directly —
/// a send failure here is logged to EmailLog and swallowed, never thrown, since a broken SMTP
/// connection must never fail the business operation that triggered the email.
/// </summary>
public interface IEmailService
{
    /// <returns>True only if the message actually went out. Most callers ignore this — the log is the
    /// record — but the ones that tell an admin what just happened need to know which it was.</returns>
    Task<bool> SendAsync<TModel>(
        string templateKey, string recipientEmail, string subject, TModel model,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default);
}
