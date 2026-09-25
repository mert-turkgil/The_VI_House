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
    /// <param name="culture">One of SiteCultures.Names — the language this recipient gets the email
    /// in. Every caller must decide it deliberately: a known ApplicationUser's PreferredCulture (or
    /// SiteCultures.Default if unset), a pre-account row's own stored PreferredCulture, or
    /// SiteCultures.Default for an internal staff alert that is never personalised.</param>
    Task<bool> SendAsync<TModel>(
        string templateKey, string recipientEmail, string subject, TModel model, string culture,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default);

    /// <summary>Sends a failed email again, exactly as it was rendered, as a new log row. The
    /// failed row is marked resent and its stored body cleared either way.</summary>
    Task<ResendResult> ResendAsync(Guid logId, CancellationToken ct = default);
}
