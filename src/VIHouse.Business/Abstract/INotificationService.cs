using VIHouse.Entities.Notifications;

namespace VIHouse.Business.Abstract;

/// <summary>
/// Brief §73. Create methods never throw — same contract as IEmailService.SendAsync: a failed
/// notification insert must never fail the application approval or payment confirmation that
/// triggered it.
/// </summary>
public interface INotificationService
{
    Task CreateForUserAsync(Guid userId, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default);

    /// <summary>Looks up the account by email internally — no-ops silently if none exists yet (e.g. a brand-new applicant who hasn't paid, so has no account to notify). Keeps ApplicationService from needing a UserManager dependency of its own.</summary>
    Task CreateForEmailAsync(string email, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default);

    Task<List<Notification>> GetForUserAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    /// <summary>No-ops if the notification doesn't belong to userId — a user can never mark someone else's notification read.</summary>
    Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The real trigger behind the "event update"/"new schedule" types — notifies every user with a Confirmed booking for the experience. Returns how many were notified. Audit-logged like every other admin mutation.</summary>
    Task<int> BroadcastToExperienceAttendeesAsync(
        Guid experienceId, NotificationType type, string title, string body, string? link,
        Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}
