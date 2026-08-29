namespace VIHouse.Business.Abstract;

/// <summary>
/// The single place that sends + logs a text message, mirroring IEmailService.
///
/// There is no template renderer here on purpose: a text message is one short line built at the call
/// site, where the length budget is visible, rather than a Razor view whose output nobody can eyeball
/// against 160 characters. What this adds over ISmsSender is the operational part — a normalised
/// number, a row in SmsLogs whatever happens, and a gateway outage that never fails the business
/// operation that triggered the message.
/// </summary>
public interface ISmsService
{
    /// <summary>False when no gateway is configured — lets callers phrase the difference between "SMS
    /// is switched off" and "the message failed".</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// True only when the message actually went out. False covers all three ways it doesn't: no
    /// gateway configured, no usable number on the record, or the gateway refusing it. Which one it
    /// was is in SmsLogs, on the Emails &amp; SMS screen.
    /// </summary>
    /// <param name="templateKey">Names the message for the log, e.g. "ApplicationApproved" — the same
    /// key its email counterpart uses, so the two channels line up on screen.</param>
    Task<bool> SendAsync(
        string templateKey, string? recipientPhone, string body,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default);
}
