using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Marketing;

namespace VIHouse.Business.Concrete;

public class NotifySignupService(
    INotifySignupRepository signups,
    IOutbox outbox,
    IEmailService emailService,
    IAuditLogRepository auditLogs,
    IOptions<SiteOptions> siteOptions) : INotifySignupService
{
    private const string TemplateKey = "LaunchAnnouncement";
    private static readonly EmailAddressAttribute EmailFormat = new();

    public async Task<string?> SubscribeAsync(string? email, string culture, string? source, string consentText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return "ComingSoon.Notify.Missing";

        // Stored lower case so the unique index means what it says. See EfNotifySignupRepository —
        // under the Turkish collation the address is not otherwise reliably comparable.
        var normalised = email.Trim().ToLowerInvariant();

        // Length before format: 320 is the column width, and an over-long value would otherwise
        // reach the database and come back as a truncation error rather than a message anyone can
        // act on.
        if (normalised.Length > 320 || !EmailFormat.IsValid(normalised)) return "ComingSoon.Notify.Invalid";

        var existing = await signups.FindByEmailAsync(normalised, ct);
        if (existing is not null) return "ComingSoon.Notify.Already";

        var signup = new NotifySignup
        {
            Email = normalised,
            Culture = SiteCultures.Normalise(culture),
            Source = source,
            ConsentText = Text.Clip(Text.NullIfBlank(consentText), 1000),
        };

        await signups.AddAsync(signup, ct);

        try
        {
            await signups.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index caught a double submit between the Find above and this write — two
            // clicks, or two tabs. Read back what actually landed rather than reporting a failure:
            // they are on the list either way, and which of the two racing requests won is not
            // their problem. Mirrors ExperienceService.JoinWaitlistAsync.
            //
            // The failed insert is still tracked as Added on this scoped DbContext, so it is
            // detached first — otherwise anything later in the same request that saves would retry
            // the insert and throw again. JoinWaitlistAsync gets away without this only because
            // nothing follows it in that request.
            signups.Remove(signup);

            return await signups.FindByEmailAsync(normalised, ct) is null
                ? "ComingSoon.Notify.Failed"
                : "ComingSoon.Notify.Already";
        }

        return null;
    }

    public Task<int> CountRecipientsAsync(bool includeAlreadyNotified, CancellationToken ct = default) =>
        includeAlreadyNotified
            ? signups.CountAsync(ct)
            : signups.CountAsync(n => n.NotifiedAt == null, ct);

    public async Task<LaunchAnnouncementResult> AnnounceAsync(
        LaunchAnnouncement message, bool includeAlreadyNotified, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (Validate(message) is { } problem) return new(false, problem);

        var recipients = includeAlreadyNotified
            ? await signups.GetAllAsync(ct)
            : await signups.FindAsync(n => n.NotifiedAt == null, ct);
        if (recipients.Count == 0)
            return new(false, includeAlreadyNotified ? "The launch list is empty." : "Everyone on the list has already been notified.");

        // One id per send, in every dedupe key: the same person can get a second announcement from a
        // second, deliberate send, but never two copies of the same one (a double click, a retry).
        var sendId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        foreach (var signup in recipients)
        {
            await outbox.EnqueueEmailAsync(
                $"email:{TemplateKey}:{sendId:N}:NotifySignup:{signup.Id}",
                TemplateKey, signup.Email, message.Subject.Trim(),
                BuildModel(message, signup.Culture, SiteUrls.LeaveLaunchList(signup.Id)),
                signup.Culture,
                nameof(NotifySignup), signup.Id, ct);
            signup.NotifiedAt = now;
            signup.UpdatedAt = now;
        }
        await signups.SaveChangesAsync(ct);

        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = "LaunchListAnnounced",
            EntityType = nameof(NotifySignup),
            DataAfter = JsonSerializer.Serialize(new
            {
                SendId = sendId,
                message.Subject,
                message.Headline,
                RecipientCount = recipients.Count,
                IncludedAlreadyNotified = includeAlreadyNotified,
            }),
            IpAddress = ipAddress,
        }, ct);
        await auditLogs.SaveChangesAsync(ct);

        return new(true, $"Queued for {recipients.Count} address(es). They go out over the next minute or two — progress and any failures are on Emails & SMS.", recipients.Count);
    }

    public async Task<LaunchAnnouncementResult> SendTestAsync(LaunchAnnouncement message, string toEmail, string culture, CancellationToken ct = default)
    {
        if (Validate(message) is { } problem) return new(false, problem);

        // A real send through the same template, so the test shows exactly what the list will get.
        // The leave link points at the list page rather than at a row, since the admin is not on it.
        var sent = await emailService.SendAsync(
            TemplateKey, toEmail, "[Test] " + message.Subject.Trim(),
            BuildModel(message, culture, "/coming-soon"),
            SiteCultures.Normalise(culture));

        return sent
            ? new(true, $"Test sent to {toEmail}. Check it, then send it to the list.", 1)
            : new(false, $"The test to {toEmail} did not go out — see Emails & SMS for the reason.");
    }

    public Task<NotifySignup?> GetAsync(Guid id, CancellationToken ct = default) => signups.GetByIdAsync(id, ct);

    public async Task<bool> LeaveAsync(Guid id, CancellationToken ct = default)
    {
        var signup = await signups.GetByIdAsync(id, ct);
        if (signup is null) return false;

        signups.Remove(signup);
        await signups.SaveChangesAsync(ct);
        return true;
    }

    private LaunchAnnouncementEmailModel BuildModel(LaunchAnnouncement message, string culture, string leavePath)
    {
        var baseUrl = siteOptions.Value.BaseUrl;
        return new LaunchAnnouncementEmailModel(
            message.Headline.Trim(),
            message.Message.Trim(),
            string.IsNullOrWhiteSpace(message.ButtonLabel) ? null : message.ButtonLabel.Trim(),
            ResolveButtonUrl(message.ButtonUrl, culture, baseUrl),
            SiteUrls.Absolute(baseUrl, SiteUrls.InCulture(leavePath, culture)));
    }

    /// <summary>A site path ("/experiences") is sent in each reader's language, on this site; a full
    /// https address is used as written; nothing at all means the home page.</summary>
    private static string ResolveButtonUrl(string? buttonUrl, string culture, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(buttonUrl)) return SiteUrls.Absolute(baseUrl, SiteUrls.InCulture(SiteUrls.Home, culture));

        var trimmed = buttonUrl.Trim();
        return IsSitePath(trimmed) ? SiteUrls.Absolute(baseUrl, SiteUrls.InCulture(trimmed, culture)) : trimmed;
    }

    private static bool IsSitePath(string url) => url.StartsWith('/') && !url.StartsWith("//");

    private static string? Validate(LaunchAnnouncement m)
    {
        if (string.IsNullOrWhiteSpace(m.Subject)) return "Give the email a subject.";
        if (m.Subject.Trim().Length > 200) return "The subject can be at most 200 characters.";
        if (string.IsNullOrWhiteSpace(m.Headline)) return "Give the email a headline.";
        if (m.Headline.Trim().Length > 200) return "The headline can be at most 200 characters.";
        if (string.IsNullOrWhiteSpace(m.Message)) return "Write the message.";
        if (m.Message.Trim().Length > 5000) return "The message can be at most 5,000 characters.";
        if (m.ButtonLabel?.Trim().Length > 60) return "The button label can be at most 60 characters.";

        if (!string.IsNullOrWhiteSpace(m.ButtonUrl))
        {
            var url = m.ButtonUrl.Trim();
            var ok = IsSitePath(url)
                     || Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
            if (!ok) return "The button link must be a site path like /experiences or a full https:// address.";
        }

        return null;
    }
}
