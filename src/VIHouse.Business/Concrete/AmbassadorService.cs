using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Referrals;

namespace VIHouse.Business.Concrete;

public class AmbassadorService(
    IAmbassadorRepository ambassadors,
    IRepository<ReferralVisit> visits,
    IApplicationRepository applications,
    IPaymentRepository payments,
    IMembershipPaymentRepository membershipPayments,
    IAuditLogRepository auditLogs,
    UserManager<ApplicationUser> userManager) : IAmbassadorService
{
    public Task<List<Ambassador>> GetAllAsync(CancellationToken ct = default) => ambassadors.GetAllAsync(ct);

    public Task<Ambassador?> GetByIdAsync(Guid id, CancellationToken ct = default) => ambassadors.GetByIdAsync(id, ct);

    public Task<Ambassador?> GetByCodeAsync(string code, CancellationToken ct = default) => ambassadors.GetByCodeAsync(code, ct);

    public Task<Ambassador?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) => ambassadors.GetByUserIdAsync(userId, ct);

    public async Task<AmbassadorCreationResult> CreateAsync(
        string email, string name, string code, decimal commissionPercent, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await ambassadors.GetByCodeAsync(code, ct) is not null)
            return AmbassadorCreationResult.Fail($"Code \"{code}\" is already in use.");

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = name,
                LastName = "",
                Country = "GB",
                MemberStatus = Entities.Users.MemberStatus.Active,
            };

            // Random, never-communicated password — same reasoning as PaymentService.ProvisionMemberAccountAsync:
            // the admin shares a password-reset link instead (built by the caller, see AdminAmbassadorsController).
            var temporaryPassword = RandomNumberGenerator.GetString(
                "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!#%", 24);

            var result = await userManager.CreateAsync(user, temporaryPassword);
            if (!result.Succeeded)
                return AmbassadorCreationResult.Fail(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Ambassador))
            await userManager.AddToRoleAsync(user, Roles.Ambassador);

        var ambassador = new Ambassador
        {
            UserId = user.Id,
            Code = code,
            Name = name,
            CommissionPercent = commissionPercent,
            Status = AmbassadorStatus.Active,
        };
        await ambassadors.AddAsync(ambassador, ct);
        await LogAsync("AmbassadorCreated", ambassador.Id, adminUserId, ipAddress, null, new { ambassador.Code, ambassador.Name }, ct);
        await ambassadors.SaveChangesAsync(ct);

        return AmbassadorCreationResult.Ok(ambassador, user.Id);
    }

    public async Task UpdateAsync(Ambassador updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await ambassadors.GetByIdAsync(updated.Id, ct)
            ?? throw new InvalidOperationException($"Ambassador {updated.Id} not found.");

        var before = new { existing.Name, existing.CommissionPercent, existing.Status };

        existing.Name = updated.Name;
        existing.CommissionPercent = updated.CommissionPercent;
        existing.Status = updated.Status;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        // Code is deliberately immutable after creation — changing it would silently orphan every
        // /r/{code} link already handed out.

        await LogAsync("AmbassadorUpdated", existing.Id, adminUserId, ipAddress,
            before, new { existing.Name, existing.CommissionPercent, existing.Status }, ct);
        await ambassadors.SaveChangesAsync(ct);
    }

    public async Task RecordVisitAsync(string code, string? utmSource, string? utmMedium, string? utmCampaign, string? utmContent, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByCodeAsync(code, ct);
        if (ambassador is null || ambassador.Status != AmbassadorStatus.Active) return;

        await visits.AddAsync(new ReferralVisit
        {
            AmbassadorId = ambassador.Id,
            UtmSource = utmSource,
            UtmMedium = utmMedium,
            UtmCampaign = utmCampaign,
            UtmContent = utmContent,
        }, ct);
        await visits.SaveChangesAsync(ct);
    }

    public async Task<AmbassadorStats> GetStatsAsync(Guid ambassadorId, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null)
            return new AmbassadorStats(0, 0, 0, 0, 0, [], []);

        var allVisits = await visits.FindAsync(v => v.AmbassadorId == ambassadorId, ct);

        var referredApplications = (await applications.GetAllAsync(ct))
            .Where(a => a.ReferralCode == ambassador.Code)
            .ToList();
        var referredApplicationIds = referredApplications.Select(a => a.Id).ToHashSet();
        var approvedCount = referredApplications.Count(a => a.Status is ApplicationStatus.Approved or ApplicationStatus.PaymentPending or ApplicationStatus.Paid);

        var referredTicketPayments = (await payments.GetAllAsync(ct))
            .Where(p => p.Status == PaymentStatus.Paid && referredApplicationIds.Contains(p.ApplicationId))
            .ToList();

        var referredMembershipPayments = (await membershipPayments.GetAllAsync(ct))
            .Where(p => p.Status == PaymentStatus.Paid && p.ReferralCode == ambassador.Code)
            .ToList();

        var revenueByCurrency = new Dictionary<string, long>();
        void AddRevenue(string currency, long amountMinor) =>
            revenueByCurrency[currency] = revenueByCurrency.GetValueOrDefault(currency) + amountMinor;

        foreach (var p in referredTicketPayments) AddRevenue(p.Currency, p.AmountMinor);
        foreach (var p in referredMembershipPayments) AddRevenue(p.Currency, p.AmountMinor);

        var commissionByCurrency = revenueByCurrency.ToDictionary(
            kv => kv.Key,
            kv => (long)Math.Round(kv.Value * ambassador.CommissionPercent / 100m, MidpointRounding.AwayFromZero));

        return new AmbassadorStats(
            Visits: allVisits.Count,
            Applications: referredApplications.Count,
            ApprovedApplications: approvedCount,
            TicketPurchases: referredTicketPayments.Count,
            MembershipPurchases: referredMembershipPayments.Count,
            RevenueByCurrency: revenueByCurrency,
            CommissionByCurrency: commissionByCurrency);
    }

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(Ambassador),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
