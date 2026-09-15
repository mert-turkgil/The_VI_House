using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Users;

namespace VIHouse.Business.Concrete;

public class SecurityAlertService(
    IRepository<SignInRecord> signIns,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IOptions<SiteOptions> siteOptions,
    ILogger<SecurityAlertService> logger) : ISecurityAlertService
{
    private static readonly TimeSpan KnownWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(90);

    private string BaseUrl => siteOptions.Value.BaseUrl.TrimEnd('/');

    public Task PasswordChangedAsync(Guid userId, string? ipAddress, string? userAgent, CancellationToken ct = default) =>
        SendAsync(userId, "Your password was changed",
            "The password on your VI House account was just changed. If that was you, there is nothing to do.",
            $"{BaseUrl}/Identity/Account/ForgotPassword", "Reset your password", ipAddress, userAgent, ct);

    public Task TwoFactorChangedAsync(Guid userId, string what, string? ipAddress, string? userAgent, CancellationToken ct = default) =>
        SendAsync(userId, what,
            "The two-step verification settings on your VI House account were just changed. If that was you, there is nothing to do.",
            $"{BaseUrl}/Identity/Account/Manage/TwoFactorAuthentication", "Review two-step verification", ipAddress, userAgent, ct);

    public async Task RecordSignInAsync(Guid userId, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var since = now - KnownWindow;
            var ip = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim();

            var recent = await signIns.FindAsync(r => r.UserId == userId && r.At >= since, ct);
            // Unknown address means "not local proxy noise we can reason about" — it is treated as
            // known rather than alerting on every sign-in behind a stripped header.
            var seenBefore = ip is null || recent.Any(r => r.IpAddress == ip);
            // The very first sign-in on a fresh account has nothing to compare against.
            var firstEver = recent.Count == 0 && (await signIns.FindAsync(r => r.UserId == userId, ct)).Count == 0;

            await signIns.AddAsync(new SignInRecord
            {
                UserId = userId,
                IpAddress = ip,
                UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Length > 300 ? userAgent[..300] : userAgent,
                At = now,
            }, ct);

            // Keep the table small: anything older than the retention window goes on the way past.
            var cutoff = now - Retention;
            foreach (var stale in await signIns.FindAsync(r => r.UserId == userId && r.At < cutoff, ct))
                signIns.Remove(stale);
            await signIns.SaveChangesAsync(ct);

            if (seenBefore || firstEver) return;

            await SendAsync(userId, "New sign-in to your account",
                "Your VI House account was just signed in to from an address it has not used in the last thirty days. If that was you — a new phone, a trip, a different network — there is nothing to do.",
                $"{BaseUrl}/Identity/Account/Manage/ChangePassword", "Change your password", ip, userAgent, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record sign-in for user {UserId}.", userId);
        }
    }

    private async Task SendAsync(Guid userId, string eventTitle, string detail, string actionUrl, string actionLabel, string? ip, string? userAgent, CancellationToken ct)
    {
        try
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user?.Email is null) return;

            await emailService.SendAsync(
                "SecurityAlert", user.Email, $"{eventTitle} — The VI House",
                new SecurityAlertEmailModel(user.FirstName, eventTitle, detail, DateTimeOffset.UtcNow,
                    ip, Summarise(userAgent), actionUrl, actionLabel),
                nameof(ApplicationUser), user.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send security alert '{Event}' for user {UserId}.", eventTitle, userId);
        }
    }

    /// <summary>A full user-agent string is a paragraph of version numbers; the browser and the
    /// platform are what a person recognises.</summary>
    private static string? Summarise(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return null;
        var browser = userAgent.Contains("Edg/") ? "Edge"
            : userAgent.Contains("OPR/") ? "Opera"
            : userAgent.Contains("Chrome/") ? "Chrome"
            : userAgent.Contains("Firefox/") ? "Firefox"
            : userAgent.Contains("Safari/") ? "Safari"
            : "Browser";
        var platform = userAgent.Contains("iPhone") ? "iPhone"
            : userAgent.Contains("iPad") ? "iPad"
            : userAgent.Contains("Android") ? "Android"
            : userAgent.Contains("Windows") ? "Windows"
            : userAgent.Contains("Mac OS") ? "Mac"
            : userAgent.Contains("Linux") ? "Linux"
            : "unknown device";
        return $"{browser} on {platform}";
    }
}
