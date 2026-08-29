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

        try
        {
            await sender.SendAsync(normalised, body, ct);
            log.Status = EmailStatus.Sent;
            log.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            // Same contract as EmailService: an unreachable gateway must never fail the operation
            // that triggered the message — approving an application, confirming a payment.
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = Truncate(ex.Message, 1000);
            logger.LogError(ex, "Failed to send SMS {TemplateKey} to {Recipient}", templateKey, normalised);
        }

        await smsLogs.SaveChangesAsync(ct);
        return log.Status == EmailStatus.Sent;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
