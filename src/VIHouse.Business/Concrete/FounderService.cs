using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Notifications;

namespace VIHouse.Business.Concrete;

/// <summary>
/// The founder programme. Membership is what makes a Founder, not a ticket: the role is granted
/// when a membership starts inside the window (or by backfill for the people who joined before the
/// programme existed), and every perk reads the role live from the database.
/// </summary>
public class FounderService(
    ISiteSettingsService siteSettings,
    IRepository<Membership> memberships,
    IRepository<MembershipPlan> plans,
    UserManager<ApplicationUser> userManager,
    IOutbox outbox,
    IAuditLogRepository auditLogs,
    IOptions<SiteOptions> siteOptions,
    ILogger<FounderService> logger) : IFounderService
{
    public async Task<FounderProgramme> GetProgrammeAsync(CancellationToken ct = default)
    {
        var s = await siteSettings.GetCachedAsync(ct);
        return new FounderProgramme(s.FounderWindowEndsAtUtc, s.FounderExtraDiscountPercent, s.FounderEarlyAccessDays, s.FounderBadgeEnabled);
    }

    public Task UpdateProgrammeAsync(FounderProgramme programme, Guid adminUserId, string? ipAddress, CancellationToken ct = default) =>
        siteSettings.UpdateFounderProgrammeAsync(programme.WindowEndsAtUtc, programme.ExtraDiscountPercent,
            programme.EarlyAccessDays, programme.BadgeEnabled, adminUserId, ipAddress, ct);

    public async Task<bool> TryGrantForMembershipAsync(Guid userId, DateTimeOffset membershipStartedAt, CancellationToken ct = default)
    {
        try
        {
            var programme = await GetProgrammeAsync(ct);
            if (!programme.IsWindowOpen(membershipStartedAt)) return false;

            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null) return false;

            return await GrantAsync(user, membershipStartedAt, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never undo the membership over a badge: log it, and the backfill button picks them up.
            logger.LogError(ex, "Founder grant failed for user {UserId}.", userId);
            return false;
        }
    }

    public async Task<int> BackfillAsync(Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var programme = await GetProgrammeAsync(ct);
        if (programme.WindowEndsAtUtc is not { } windowEnd) return 0;

        // Every membership counts, including ones since cancelled or expired: someone who joined
        // at launch and let it lapse was still there at the beginning.
        var all = await memberships.FindAsync(m => m.StartAt < windowEnd, ct);
        var firstStartByUser = all.GroupBy(m => m.UserId).ToDictionary(g => g.Key, g => g.Min(m => m.StartAt));
        if (firstStartByUser.Count == 0) return 0;

        var existing = (await userManager.GetUsersInRoleAsync(Roles.Founder)).Select(u => u.Id).ToHashSet();
        var granted = 0;

        foreach (var (userId, firstStart) in firstStartByUser)
        {
            if (existing.Contains(userId)) continue;
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null) continue;
            if (await GrantAsync(user, firstStart, ct)) granted++;
        }

        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = "FoundersBackfilled",
            EntityType = "FounderProgramme",
            EntityId = Guid.Empty,
            DataAfter = JsonSerializer.Serialize(new { Granted = granted, WindowEndsAtUtc = windowEnd }),
            IpAddress = ipAddress,
        }, ct);
        await auditLogs.SaveChangesAsync(ct);

        return granted;
    }

    public async Task MarkGrantedManuallyAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.FounderSince is not null) return;

        user.FounderSince = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);
        await WelcomeAsync(user, ct);
    }

    public async Task<FounderPerks> GetPerksAsync(Guid? userId, CancellationToken ct = default)
    {
        if (userId is null) return FounderPerks.None;
        var user = await userManager.FindByIdAsync(userId.Value.ToString());
        if (user is null || !await userManager.IsInRoleAsync(user, Roles.Founder)) return FounderPerks.None;

        var programme = await GetProgrammeAsync(ct);
        return new FounderPerks(true, programme.ExtraDiscountPercent, programme.EarlyAccessDays, programme.BadgeEnabled);
    }

    public async Task<List<FounderListItem>> GetFoundersAsync(CancellationToken ct = default)
    {
        var founders = await userManager.GetUsersInRoleAsync(Roles.Founder);
        if (founders.Count == 0) return [];

        var ids = founders.Select(f => f.Id).ToList();
        var current = await memberships.FindAsync(m => ids.Contains(m.UserId)
            && (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.PastDue || m.Status == MembershipStatus.Trial), ct);
        var planNames = (await plans.GetAllAsync(ct)).ToDictionary(p => p.Id, p => p.Name);
        var planByUser = current.GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => planNames.GetValueOrDefault(g.OrderByDescending(m => m.StartAt).First().PlanId));

        return founders
            .OrderBy(f => f.FounderSince ?? DateTimeOffset.MaxValue)
            .Select(f => new FounderListItem(f.Id, $"{f.FirstName} {f.LastName}".Trim(), f.Email ?? "", f.FounderSince, planByUser.GetValueOrDefault(f.Id)))
            .ToList();
    }

    private async Task<bool> GrantAsync(ApplicationUser user, DateTimeOffset since, CancellationToken ct)
    {
        if (await userManager.IsInRoleAsync(user, Roles.Founder)) return false;

        var result = await userManager.AddToRoleAsync(user, Roles.Founder);
        if (!result.Succeeded)
        {
            logger.LogError("Could not add Founder role to {UserId}: {Errors}", user.Id,
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return false;
        }

        user.FounderSince ??= since;
        await userManager.UpdateAsync(user);
        await WelcomeAsync(user, ct);
        logger.LogInformation("User {UserId} is now a Founder (since {Since}).", user.Id, since);
        return true;
    }

    private async Task WelcomeAsync(ApplicationUser user, CancellationToken ct)
    {
        var baseUrl = siteOptions.Value.BaseUrl;
        if (user.Email is not null)
        {
            await outbox.EnqueueEmailAsync(
                $"email:FounderWelcome:User:{user.Id}", "FounderWelcome", user.Email, "Welcome, Founder",
                new FounderWelcomeEmailModel(user.FirstName,
                    SiteUrls.Absolute(baseUrl, SiteUrls.Account),
                    SiteUrls.Absolute(baseUrl, SiteUrls.AccountBenefits)),
                user.PreferredCulture ?? SiteCultures.Default, "User", user.Id, ct);
        }

        await outbox.EnqueueNotificationAsync(
            $"notification:FounderWelcome:User:{user.Id}", user.Id, NotificationType.AccountUpdate,
            "You're a Founder", "You joined The VI House at launch — your Founder status is permanent.",
            SiteUrls.AccountBenefits, "User", user.Id, ct);
    }
}
