using VIHouse.Business;
using VIHouse.Business.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Notifications;

namespace VIHouse.Business.Concrete;

public class NotificationService(
    INotificationRepository notifications,
    IBookingRepository bookings,
    IExperienceRepository experiences,
    IAuditLogRepository auditLogs,
    IEmailService emailService,
    IOptions<SiteOptions> siteOptions,
    UserManager<ApplicationUser> userManager,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task CreateForUserAsync(Guid userId, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default)
    {
        try
        {
            await notifications.AddAsync(new Notification { UserId = userId, Type = type, Title = title, Body = body, Link = link }, ct);
            await notifications.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Never let a failed notification insert fail the payment/approval that triggered it —
            // same contract as IEmailService.SendAsync.
            logger.LogError(ex, "Failed to create notification {Type} for user {UserId}", type, userId);
        }
    }

    public async Task CreateForEmailAsync(string email, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default)
    {
        try
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null) return; // no account yet (e.g. a brand-new applicant) — the email is the only channel for them right now

            await CreateForUserAsync(user.Id, type, title, body, link, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notification {Type} for email {Email}", type, email);
        }
    }

    public Task<List<Notification>> GetForUserAsync(Guid userId, CancellationToken ct = default) =>
        notifications.GetByUserAsync(userId, ct);

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        notifications.GetUnreadCountAsync(userId, ct);

    public async Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        var notification = await notifications.GetByIdAsync(notificationId, ct);
        if (notification is null || notification.UserId != userId) return; // not theirs — silent no-op, not a 404/403 leak

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTimeOffset.UtcNow;
            await notifications.SaveChangesAsync(ct);
        }
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var mine = await notifications.GetByUserAsync(userId, ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var n in mine.Where(n => !n.IsRead))
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        await notifications.SaveChangesAsync(ct);
    }

    public async Task<int> BroadcastToExperienceAttendeesAsync(
        Guid experienceId, NotificationType type, string title, string body, string? link,
        Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var attendeeUserIds = (await bookings.GetByExperienceAsync(experienceId, ct))
            .Where(b => b.Status == BookingStatus.Confirmed)
            .Select(b => b.UserId)
            .Distinct()
            .ToList();

        // The same words in the inbox as in the bell: an attendee who does not open the site before
        // the change matters still hears about it. The experience page is the link in both.
        var experience = await experiences.GetByIdAsync(experienceId, ct);
        var experienceUrl = experience is null ? null : SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.Experience(experience.Slug));

        foreach (var userId in attendeeUserIds)
        {
            await CreateForUserAsync(userId, type, title, body, link ?? (experience is null ? null : SiteUrls.Experience(experience.Slug)), ct);

            if (experience is null) continue;
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user?.Email is null) continue;
            await emailService.SendAsync("ExperienceUpdate", user.Email, $"{experience.Title}: {title}",
                new ExperienceUpdateEmailModel(user.FirstName, experience.Title, title, body, experienceUrl!),
                "Experience", experienceId, ct);
        }

        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = "NotificationBroadcast",
            EntityType = "Experience",
            EntityId = experienceId,
            DataAfter = JsonSerializer.Serialize(new { Type = type.ToString(), Title = title, RecipientCount = attendeeUserIds.Count }),
            IpAddress = ipAddress,
        }, ct);
        await auditLogs.SaveChangesAsync(ct);

        return attendeeUserIds.Count;
    }
}
