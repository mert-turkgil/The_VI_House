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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VIHouse.Business.Options;
using VIHouse.Entities.Notifications;

namespace VIHouse.Business.Concrete;

public class AmbassadorService(
    IAmbassadorRepository ambassadors,
    IRepository<ReferralVisit> visits,
    IApplicationRepository applications,
    IPaymentRepository payments,
    IMembershipPaymentRepository membershipPayments,
    IAuditLogRepository auditLogs,
    IRepository<ReferralConversion> conversions,
    INotificationService notificationService,
    IEmailService emailService,
    IOptions<SiteOptions> siteOptions,
    ILogger<AmbassadorService> logger,
    UserManager<ApplicationUser> userManager) : IAmbassadorService
{
    public async Task RecordConversionAsync(string? referralCode, ReferralConversionKind kind, string sourceEntityType, Guid sourceEntityId,
        long? amountMinor = null, string? currency = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referralCode)) return;
        try
        {
            var ambassador = await ambassadors.GetByCodeAsync(referralCode.Trim(), ct);
            if (ambassador is null) return;

            var already = await conversions.FindAsync(
                c => c.SourceEntityType == sourceEntityType && c.SourceEntityId == sourceEntityId && c.Kind == kind, ct);
            if (already.Count > 0) return;

            long? commission = amountMinor is { } amount
                ? (long)Math.Round(amount * ambassador.CommissionPercent / 100m, MidpointRounding.AwayFromZero)
                : null;

            await conversions.AddAsync(new ReferralConversion
            {
                AmbassadorId = ambassador.Id,
                Kind = kind,
                OccurredAt = DateTimeOffset.UtcNow,
                AmountMinor = amountMinor,
                Currency = currency,
                CommissionMinor = commission,
                SourceEntityType = sourceEntityType,
                SourceEntityId = sourceEntityId,
            }, ct);
            await conversions.SaveChangesAsync(ct);

            var what = kind switch
            {
                ReferralConversionKind.Application => "Someone who came through your link has applied to an experience.",
                ReferralConversionKind.Approved => "An application that came through your link has been approved.",
                ReferralConversionKind.TicketPurchase => "Someone who came through your link has bought a ticket.",
                _ => "Someone who came through your link has become a member.",
            };
            var amountText = amountMinor is { } a && currency is not null ? FormatMoney(a, currency) : null;
            var commissionText = commission is { } c && currency is not null ? FormatMoney(c, currency) : null;

            await notificationService.CreateForUserAsync(ambassador.UserId, NotificationType.ReferralConverted,
                "Your link just worked",
                amountText is null ? what : $"{what} {amountText}{(commissionText is null ? "" : $" — your commission {commissionText}")}.",
                "/ambassador", ct);

            var user = await userManager.FindByIdAsync(ambassador.UserId.ToString());
            if (user?.Email is not null)
            {
                await emailService.SendAsync("ReferralConverted", user.Email, "Your referral link just worked",
                    new ReferralConvertedEmailModel(ambassador.Name, what, amountText, commissionText,
                        $"{siteOptions.Value.BaseUrl.TrimEnd('/')}/ambassador"),
                    nameof(Ambassador), ambassador.Id, ct);
            }
        }
        catch (Exception ex)
        {
            // The purchase or approval that triggered this has already happened; a ledger hiccup
            // must not roll it back or surface to the customer.
            logger.LogError(ex, "Failed to record referral conversion {Kind} for code {Code} ({SourceType} {SourceId}).",
                kind, referralCode, sourceEntityType, sourceEntityId);
        }
    }

    public async Task<List<ReferralSourceCount>> GetVisitSourcesAsync(Guid ambassadorId, CancellationToken ct = default) =>
        (await visits.FindAsync(v => v.AmbassadorId == ambassadorId, ct))
            .GroupBy(v => (Source: v.UtmSource?.Trim().ToLowerInvariant(), Medium: v.UtmMedium?.Trim().ToLowerInvariant()))
            .Select(g => new ReferralSourceCount(g.Key.Source, g.Key.Medium, g.Count()))
            .OrderByDescending(s => s.Visits)
            .ToList();

    public async Task<List<ReferralConversion>> GetConversionsAsync(Guid ambassadorId, int take = 50, CancellationToken ct = default) =>
        (await conversions.FindAsync(c => c.AmbassadorId == ambassadorId, ct))
            .OrderByDescending(c => c.OccurredAt)
            .Take(take)
            .ToList();

    /// <summary>Minor units to "£1,500.00" — the same shape the site's MoneyFormatter produces,
    /// kept here because the Business layer cannot reach the WebUI helper.</summary>
    private static string FormatMoney(long minor, string currency)
    {
        var symbol = currency.ToUpperInvariant() switch { "GBP" => "£", "EUR" => "€", "USD" => "$", var c => c + " " };
        return $"{symbol}{minor / 100m:N2}";
    }

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
