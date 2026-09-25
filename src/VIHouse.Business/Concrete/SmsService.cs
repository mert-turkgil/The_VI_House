using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Communication;

namespace VIHouse.Business.Concrete;

public class SmsService(
    ISmsSender sender,
    ISmsLogRepository smsLogs,
    IOptions<SmsOptions> options,
    ILogger<SmsService> logger) : ISmsService
{
    public bool IsConfigured => sender.IsConfigured;

    public async Task<bool> SendAsync(
        string templateKey, string? recipientPhone, string body,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default)
    {
        // No gateway at all is a configuration state, not an incident. Logging a failed row per
        // approval would bury the real failures on a screen an admin reads to find exactly those.
        if (!sender.IsConfigured) return false;

        var normalised = PhoneNumber.TryNormalise(recipientPhone, options.Value.DefaultCountryCode);

        var log = new SmsLog
        {
            TemplateKey = templateKey,
            RecipientPhone = Truncate(normalised ?? (string.IsNullOrWhiteSpace(recipientPhone) ? "—" : recipientPhone.Trim()), 40),
            Status = EmailStatus.Queued,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
        };

        if (normalised is null)
        {
            // Written down rather than dropped: "we never had a number we could send to" is the
            // answer to "why didn't they get the text", and it is only findable if it is recorded.
            // No body kept: without a usable number a resend would fail the same way.
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = string.IsNullOrWhiteSpace(recipientPhone)
                ? "No phone number on the record."
                : "Phone number isn't in a form the gateway accepts — it needs a country code.";

            await smsLogs.AddAsync(log, ct);
            await smsLogs.SaveChangesAsync(ct);
            return false;
        }

        await smsLogs.AddAsync(log, ct);
        await smsLogs.SaveChangesAsync(ct); // persist Queued first — a failure below must still leave a trail

        await DeliverAsync(log, normalised, body, ct);
        return log.Status == EmailStatus.Sent;
    }

    public async Task<ResendResult> ResendAsync(Guid logId, CancellationToken ct = default)
    {
        if (!sender.IsConfigured) return ResendResult.Refused("No SMS gateway is configured, so nothing can be texted.");

        var failed = await smsLogs.GetByIdAsync(logId, ct);
        if (failed is null) return ResendResult.Refused("That text message is no longer in the log.");
        if (failed.Status != EmailStatus.Failed) return ResendResult.Refused("Only a failed text message can be resent.");
        if (failed.ResentAt is not null) return ResendResult.Refused("That text has already been resent — see the newer row.");
        if (string.IsNullOrEmpty(failed.Body))
            return ResendResult.Refused("There is no stored copy of this text to resend (no usable number, or it predates resending).");

        var body = failed.Body;
        var attempt = new SmsLog
        {
            TemplateKey = failed.TemplateKey,
            RecipientPhone = failed.RecipientPhone,
            Status = EmailStatus.Queued,
            RelatedEntityType = failed.RelatedEntityType,
            RelatedEntityId = failed.RelatedEntityId,
        };
        failed.ResentAt = DateTimeOffset.UtcNow;
        failed.Body = null;
        await smsLogs.AddAsync(attempt, ct);
        await smsLogs.SaveChangesAsync(ct);

        await DeliverAsync(attempt, attempt.RecipientPhone, body, ct);
        return attempt.Status == EmailStatus.Sent
            ? ResendResult.Ok($"Resent to {attempt.RecipientPhone}.")
            : ResendResult.Refused($"Resending to {attempt.RecipientPhone} failed again: {attempt.ErrorMessage}");
    }

    private async Task DeliverAsync(SmsLog log, string phone, string body, CancellationToken ct)
    {
        try
        {
            await sender.SendAsync(phone, body, ct);
            log.Status = EmailStatus.Sent;
            log.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            // Same contract as EmailService: an unreachable gateway must never fail the operation
            // that triggered the message — approving an application, confirming a payment. The text
            // is kept so it can be resent from the admin log.
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = Truncate(ex.Message, 1000);
            log.Body = Truncate(body, 1600);
            logger.LogError(ex, "Failed to send SMS {TemplateKey} to {Recipient}", log.TemplateKey, phone);
        }

        await smsLogs.SaveChangesAsync(ct);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
