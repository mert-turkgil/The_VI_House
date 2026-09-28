using VIHouse.Business;
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
using VIHouse.Entities.Seminars;
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
    IRepository<ReferralPayout> payouts,
    IExperienceRepository experiences,
    ISeminarRepository seminars,
    ISeminarEnrollmentRepository seminarEnrollments,
    INotificationService notificationService,
    IEmailService emailService,
    IOptions<SiteOptions> siteOptions,
    ILogger<AmbassadorService> logger,
    UserManager<ApplicationUser> userManager) : IAmbassadorService
{
    public async Task RecordConversionAsync(string? referralCode, ReferralConversionKind kind, string sourceEntityType, Guid sourceEntityId,
        long? amountMinor = null, string? currency = null,
        ReferralTargetKind targetKind = ReferralTargetKind.Site, Guid? targetId = null,
        Guid? buyerUserId = null, string? buyerEmail = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referralCode)) return;
        try
        {
            var ambassador = await ambassadors.GetByCodeAsync(referralCode.Trim(), ct);
            // Same rule as RecordVisitAsync: a switched-off ambassador earns nothing new. The code
            // stays on the application/payment row for the record; it just does not reach the ledger.
            if (ambassador is null || ambassador.Status != AmbassadorStatus.Active) return;

            var already = await conversions.FindAsync(
                c => c.SourceEntityType == sourceEntityType && c.SourceEntityId == sourceEntityId && c.Kind == kind, ct);
            if (already.Count > 0) return;

            long? commission = amountMinor is { } amount
                ? (long)Math.Round(amount * ambassador.CommissionPercent / 100m, MidpointRounding.AwayFromZero)
                : null;

            // Buying through your own link is flagged, not refused: the purchase is real, and
            // whether it earns commission is the House's call before the payout.
            var ambassadorUser = await userManager.FindByIdAsync(ambassador.UserId.ToString());
            var selfReferral = (buyerUserId is { } buyer && buyer == ambassador.UserId)
                || (!string.IsNullOrWhiteSpace(buyerEmail) && ambassadorUser?.Email is { } ownEmail
                    && string.Equals(buyerEmail.Trim(), ownEmail, StringComparison.OrdinalIgnoreCase));

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
                TargetKind = targetKind,
                TargetId = targetId,
                IsSelfReferral = selfReferral,
            }, ct);
            await conversions.SaveChangesAsync(ct);
            if (selfReferral)
                logger.LogWarning("Self-referral: ambassador {AmbassadorId} bought through their own code ({SourceType} {SourceId}).",
                    ambassador.Id, sourceEntityType, sourceEntityId);

            var what = kind switch
            {
                ReferralConversionKind.Application => "Someone who came through your link has applied to an experience.",
                ReferralConversionKind.Approved => "An application that came through your link has been approved.",
                ReferralConversionKind.TicketPurchase => "Someone who came through your link has bought a ticket.",
                ReferralConversionKind.SessionPurchase => "Someone who came through your link has bought a place on a session.",
                _ => "Someone who came through your link has become a member.",
            };
            var amountText = amountMinor is { } a && currency is not null ? FormatMoney(a, currency) : null;
            var commissionText = commission is { } c && currency is not null ? FormatMoney(c, currency) : null;

            await notificationService.CreateForUserAsync(ambassador.UserId, NotificationType.ReferralConverted,
                "Your link just worked",
                amountText is null ? what : $"{what} {amountText}{(commissionText is null ? "" : $" — your commission {commissionText}")}.",
                SiteUrls.Ambassador, ct);

            var user = ambassadorUser;
            if (user?.Email is not null)
            {
                await emailService.SendAsync("ReferralConverted", user.Email, "Your referral link just worked",
                    new ReferralConvertedEmailModel(ambassador.Name, what, amountText, commissionText,
                        SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.Ambassador)),
                    user.PreferredCulture ?? SiteCultures.Default,
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

    public async Task<bool> RecordVisitAsync(string code, ReferralTargetKind targetKind, Guid? targetId, string? landingPath,
        string? utmSource, string? utmMedium, string? utmCampaign, string? utmContent,
        ReferralVisitor? visitor = null, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByCodeAsync(code, ct);
        if (ambassador is null || ambassador.Status != AmbassadorStatus.Active) return false;

        // One network, one link, one visit per window: a refresh, a back button or a script
        // hammering the link is not new interest.
        if (visitor?.IpHash is { } ipHash)
        {
            var since = DateTimeOffset.UtcNow - IAmbassadorService.RepeatVisitWindow;
            var recent = await visits.FindAsync(v => v.AmbassadorId == ambassador.Id && v.IpHash == ipHash && v.CreatedAt >= since
                && v.TargetKind == targetKind && v.TargetId == targetId, ct);
            if (recent.Count > 0) return false;
        }

        await visits.AddAsync(new ReferralVisit
        {
            AmbassadorId = ambassador.Id,
            TargetKind = targetKind,
            TargetId = targetId,
            LandingPath = landingPath is { Length: > 200 } ? landingPath[..200] : landingPath,
            UtmSource = Clip(utmSource),
            UtmMedium = Clip(utmMedium),
            UtmCampaign = Clip(utmCampaign),
            UtmContent = Clip(utmContent),
            IpHash = visitor?.IpHash,
            UserAgent = visitor?.UserAgent is { Length: > 0 } ua ? (ua.Length > 300 ? ua[..300] : ua) : null,
            VisitorUserId = visitor?.UserId,
        }, ct);
        await visits.SaveChangesAsync(ct);
        return true;

        // The columns are 100 wide and the values come straight off a query string.
        static string? Clip(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Length > 100 ? value[..100] : value;
    }

    public async Task<List<ReferralLinkTarget>> GetLinkTargetsAsync(CancellationToken ct = default)
    {
        var targets = new List<ReferralLinkTarget>();

        foreach (var e in await experiences.GetPublicListingAsync(new ExperienceFilter { Take = 200 }, ct))
        {
            targets.Add(new ReferralLinkTarget(ReferralTargetKind.Experience, e.Id, e.Slug,
                ExperienceContent.Title(e, SiteCultures.Default), e.City, e.StartAtUtc));
        }

        // Members-only sessions are included: the link is a way in for someone who then joins, and
        // the page itself explains what it takes to attend.
        foreach (var s in await seminars.GetPublicListingAsync(new SeminarFilter { IncludeMembersOnly = true, Take = 200 }, ct))
        {
            targets.Add(new ReferralLinkTarget(ReferralTargetKind.Session, s.Id, s.Slug,
                SeminarContent.Title(s, SiteCultures.Default), s.IsOnline ? "Online" : s.Location, s.StartAtUtc));
        }

        // Soonest sitting first; on-demand sessions (no date) last.
        return targets.OrderBy(t => t.StartAtUtc is null).ThenBy(t => t.StartAtUtc).ToList();
    }

    public async Task<AmbassadorStats> GetStatsAsync(Guid ambassadorId, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null)
            return new AmbassadorStats(0, 0, 0, 0, 0, 0, [], [], []);

        var allVisits = await visits.FindAsync(v => v.AmbassadorId == ambassadorId, ct);

        var referredApplications = (await applications.GetAllAsync(ct))
            .Where(a => a.ReferralCode == ambassador.Code)
            .ToList();
        var referredApplicationIds = referredApplications.Select(a => a.Id).ToHashSet();
        var approvedCount = referredApplications.Count(a => a.Status is ApplicationStatus.Approved or ApplicationStatus.PaymentPending or ApplicationStatus.Paid);

        var referredTicketPayments = (await payments.GetAllAsync(ct))
            .Where(p => p.Status is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded && referredApplicationIds.Contains(p.ApplicationId))
            .ToList();

        var referredMembershipPayments = (await membershipPayments.GetAllAsync(ct))
            .Where(p => p.Status is PaymentStatus.Paid or PaymentStatus.PartiallyRefunded && p.ReferralCode == ambassador.Code)
            .ToList();

        var referredSessionPurchases = (await seminarEnrollments.GetAllAsync(ct))
            .Where(e => e.ReferralCode == ambassador.Code && e.Status == SeminarEnrollmentStatus.Confirmed && e.GrantedVia == SeminarAccessGrant.Purchase)
            .ToList();

        // Money comes from the ledger, not from re-pricing today's payments: each line carries the
        // rate in force when it happened, less whatever a refund or a void took back. Changing the
        // commission % therefore never rewrites what was already earned.
        var ledger = await conversions.FindAsync(c => c.AmbassadorId == ambassadorId, ct);
        var purchaseLines = ledger.Where(c => c.AmountMinor is not null && c.Currency is not null && c.ReversedAt is null).ToList();
        var revenueByCurrency = purchaseLines
            .GroupBy(c => c.Currency!)
            .ToDictionary(g => g.Key, g => g.Sum(c => Math.Max(0, c.AmountMinor!.Value - c.RefundedMinor)));
        var commissionByCurrency = ledger
            .Where(c => c.Currency is not null && c.CommissionMinor is not null)
            .GroupBy(c => c.Currency!)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.NetCommissionMinor));
        var paidByCurrency = (await payouts.FindAsync(p => p.AmbassadorId == ambassadorId, ct))
            .GroupBy(p => p.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountMinor));
        var balances = commissionByCurrency.Keys.Union(paidByCurrency.Keys)
            .OrderBy(c => c)
            .Select(c => new CommissionBalance(c, commissionByCurrency.GetValueOrDefault(c), paidByCurrency.GetValueOrDefault(c)))
            .ToList();

        var targets = await BuildTargetStatsAsync(ambassadorId, allVisits, ct);

        return new AmbassadorStats(
            Visits: allVisits.Count,
            Applications: referredApplications.Count,
            ApprovedApplications: approvedCount,
            TicketPurchases: referredTicketPayments.Count,
            MembershipPurchases: referredMembershipPayments.Count,
            SessionPurchases: referredSessionPurchases.Count,
            RevenueByCurrency: revenueByCurrency,
            CommissionByCurrency: commissionByCurrency,
            Targets: targets)
        {
            Balances = balances,
        };
    }

    /// <summary>
    /// Per link: which post brought the visits, and what those visits turned into. Visits are
    /// grouped by the target recorded at /r/{code}/…, conversions by the target on the ledger row —
    /// so a ticket bought after an experience-scoped link counts under that experience even if the
    /// buyer wandered around the site first.
    /// </summary>
    private async Task<List<ReferralTargetStats>> BuildTargetStatsAsync(Guid ambassadorId, List<ReferralVisit> allVisits, CancellationToken ct)
    {
        var ledger = await conversions.FindAsync(c => c.AmbassadorId == ambassadorId, ct);
        var keys = allVisits.Select(v => (v.TargetKind, v.TargetId))
            .Concat(ledger.Select(c => (c.TargetKind, c.TargetId)))
            .Distinct()
            .ToList();

        var experienceIds = keys.Where(k => k.TargetKind == ReferralTargetKind.Experience && k.TargetId is not null).Select(k => k.TargetId!.Value).ToHashSet();
        var seminarIds = keys.Where(k => k.TargetKind == ReferralTargetKind.Session && k.TargetId is not null).Select(k => k.TargetId!.Value).ToList();
        var experiencesById = experienceIds.Count == 0
            ? new Dictionary<Guid, Entities.Experiences.Experience>()
            : (await experiences.GetAllAsync(ct)).Where(e => experienceIds.Contains(e.Id)).ToDictionary(e => e.Id);
        var seminarsById = seminarIds.Count == 0
            ? new Dictionary<Guid, Seminar>()
            : (await seminars.GetByIdsAsync(seminarIds, ct)).ToDictionary(s => s.Id);

        var targets = new List<ReferralTargetStats>();
        foreach (var (kind, id) in keys)
        {
            string title;
            string? slug = null;
            if (kind == ReferralTargetKind.Experience && id is not null && experiencesById.TryGetValue(id.Value, out var experience))
            {
                title = ExperienceContent.Title(experience, SiteCultures.Default);
                slug = experience.Slug;
            }
            else if (kind == ReferralTargetKind.Session && id is not null && seminarsById.TryGetValue(id.Value, out var seminar))
            {
                title = SeminarContent.Title(seminar, SiteCultures.Default);
                slug = seminar.Slug;
            }
            else
            {
                title = kind switch
                {
                    ReferralTargetKind.Experience => "An experience that is no longer listed",
                    ReferralTargetKind.Session => "A session that is no longer listed",
                    _ => "Site link",
                };
            }

            targets.Add(new ReferralTargetStats(kind, id, title, slug,
                Visits: allVisits.Count(v => v.TargetKind == kind && v.TargetId == id),
                Applications: ledger.Count(c => c.Kind == ReferralConversionKind.Application && c.TargetKind == kind && c.TargetId == id),
                Purchases: ledger.Count(c => c.TargetKind == kind && c.TargetId == id && c.ReversedAt is null
                    && c.Kind is ReferralConversionKind.TicketPurchase or ReferralConversionKind.MembershipPurchase or ReferralConversionKind.SessionPurchase)));
        }

        return targets.OrderByDescending(t => t.Purchases).ThenByDescending(t => t.Applications).ThenByDescending(t => t.Visits).ToList();
    }

    public async Task ReverseForRefundAsync(string sourceEntityType, Guid sourceEntityId, long refundedMinor, bool full, CancellationToken ct = default)
    {
        try
        {
            var lines = await conversions.FindAsync(c => c.SourceEntityType == sourceEntityType && c.SourceEntityId == sourceEntityId
                && c.AmountMinor != null, ct);
            foreach (var line in lines)
            {
                var amount = line.AmountMinor!.Value;
                var refunded = Math.Min(amount, Math.Max(line.RefundedMinor, full ? amount : refundedMinor));
                var commission = line.CommissionMinor ?? 0;
                var reversed = line.VoidedAt is not null || refunded >= amount || amount == 0
                    ? commission
                    : (long)Math.Round(commission * (decimal)refunded / amount, MidpointRounding.AwayFromZero);
                reversed = Math.Max(line.CommissionReversedMinor, reversed);

                var nowFull = refunded >= amount && line.ReversedAt is null;
                if (refunded == line.RefundedMinor && reversed == line.CommissionReversedMinor && !nowFull) continue;

                var takenBack = reversed - line.CommissionReversedMinor;
                line.RefundedMinor = refunded;
                line.CommissionReversedMinor = reversed;
                if (nowFull) line.ReversedAt = DateTimeOffset.UtcNow;
                line.UpdatedAt = DateTimeOffset.UtcNow;
                await conversions.SaveChangesAsync(ct);

                logger.LogInformation("Referral line {LineId} ({SourceType} {SourceId}) refunded {Refunded}/{Amount}; commission reversed {Reversed}{AfterPayout}.",
                    line.Id, sourceEntityType, sourceEntityId, refunded, amount, reversed, line.PayoutId is null ? "" : " after payout");

                var ambassador = await ambassadors.GetByIdAsync(line.AmbassadorId, ct);
                if (ambassador is not null && takenBack > 0 && line.Currency is not null)
                {
                    await notificationService.CreateForUserAsync(ambassador.UserId, NotificationType.ReferralConverted,
                        "A referred purchase was refunded",
                        $"{(refunded >= amount ? "A purchase" : "Part of a purchase")} made through your link was refunded, so {FormatMoney(takenBack, line.Currency)} of commission no longer stands.",
                        SiteUrls.Ambassador, ct);
                }
            }
        }
        catch (Exception ex)
        {
            // Same rule as recording: the refund itself has already happened and must not fail on this.
            logger.LogError(ex, "Failed to reverse referral commission for {SourceType} {SourceId}.", sourceEntityType, sourceEntityId);
        }
    }

    public async Task<bool> VoidConversionAsync(Guid ambassadorId, Guid conversionId, string reason, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var line = await conversions.GetByIdAsync(conversionId, ct);
        if (line is null || line.AmbassadorId != ambassadorId || line.VoidedAt is not null || line.CommissionMinor is null) return false;

        var before = new { line.CommissionMinor, line.CommissionReversedMinor, line.PayoutId };
        line.VoidedAt = DateTimeOffset.UtcNow;
        line.VoidedByAdminId = adminUserId;
        line.VoidReason = reason.Length > 300 ? reason[..300] : reason;
        line.CommissionReversedMinor = line.CommissionMinor.Value;
        line.UpdatedAt = DateTimeOffset.UtcNow;
        await LogAsync("ReferralCommissionVoided", ambassadorId, adminUserId, ipAddress, before,
            new { ConversionId = line.Id, line.VoidReason, line.CommissionReversedMinor }, ct);
        await conversions.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ReferralPayoutResult> MarkCommissionPaidAsync(Guid ambassadorId, string currency, long expectedOwedMinor,
        string? reference, string? note, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        currency = currency.Trim().ToUpperInvariant();
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null) return ReferralPayoutResult.Fail("Ambassador not found.");

        var ledger = await conversions.FindAsync(c => c.AmbassadorId == ambassadorId && c.Currency == currency && c.CommissionMinor != null, ct);
        var earned = ledger.Sum(c => c.NetCommissionMinor);
        var paid = (await payouts.FindAsync(p => p.AmbassadorId == ambassadorId && p.Currency == currency, ct)).Sum(p => p.AmountMinor);
        var owed = earned - paid;

        if (owed != expectedOwedMinor)
            return ReferralPayoutResult.Fail($"The balance changed to {FormatMoney(owed, currency)} while you were looking (a refund, a sale or another admin's payout). Nothing was recorded — check the figures and try again.");
        if (owed <= 0)
            return ReferralPayoutResult.Fail($"Nothing is owed in {currency}.");

        var payout = new ReferralPayout
        {
            AmbassadorId = ambassadorId,
            Currency = currency,
            AmountMinor = owed,
            PaidAt = DateTimeOffset.UtcNow,
            PaidByAdminId = adminUserId,
            Reference = Clip(reference, 100),
            Note = Clip(note, 500),
        };
        await payouts.AddAsync(payout, ct);
        foreach (var line in ledger.Where(c => c.PayoutId is null))
        {
            line.PayoutId = payout.Id;
            line.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await LogAsync("ReferralCommissionPaid", ambassadorId, adminUserId, ipAddress, new { Owed = owed, Currency = currency },
            new { PayoutId = payout.Id, payout.AmountMinor, payout.Currency, payout.Reference }, ct);
        await payouts.SaveChangesAsync(ct);

        await notificationService.CreateForUserAsync(ambassador.UserId, NotificationType.ReferralConverted,
            "Commission paid", $"The House has paid you {FormatMoney(owed, currency)} in commission{(payout.Reference is null ? "" : $" (reference {payout.Reference})")}.",
            SiteUrls.Ambassador, ct);
        return ReferralPayoutResult.Ok(payout);

        static string? Clip(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length > max ? value.Trim()[..max] : value.Trim();
    }

    public async Task<List<ReferralPayout>> GetPayoutsAsync(Guid ambassadorId, CancellationToken ct = default) =>
        (await payouts.FindAsync(p => p.AmbassadorId == ambassadorId, ct)).OrderByDescending(p => p.PaidAt).ToList();

    public async Task<ReferralFraudSignals> GetFraudSignalsAsync(Guid ambassadorId, CancellationToken ct = default)
    {
        const int windowDays = 30;
        var since = DateTimeOffset.UtcNow.AddDays(-windowDays);
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        var recent = await visits.FindAsync(v => v.AmbassadorId == ambassadorId && v.CreatedAt >= since, ct);
        var hashed = recent.Where(v => v.IpHash is not null).ToList();
        var top = hashed.GroupBy(v => v.IpHash).Select(g => g.Count()).DefaultIfEmpty(0).Max();
        var selfReferrals = (await conversions.FindAsync(c => c.AmbassadorId == ambassadorId && c.IsSelfReferral && c.VoidedAt == null, ct)).Count;

        return new ReferralFraudSignals(
            WindowDays: windowDays,
            Visits: recent.Count,
            DistinctNetworks: hashed.Select(v => v.IpHash).Distinct().Count(),
            TopNetworkVisits: top,
            // Only rows recorded since the fingerprint was captured can be judged.
            AutomatedVisits: hashed.Count(v => LooksAutomated(v.UserAgent)),
            OwnVisits: ambassador is null ? 0 : recent.Count(v => v.VisitorUserId == ambassador.UserId),
            SelfReferrals: selfReferrals);
    }

    /// <summary>No User-Agent at all, or one a script, crawler or headless browser sends.</summary>
    private static bool LooksAutomated(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent) || userAgent.Length < 10
        || AutomatedAgents.Any(a => userAgent.Contains(a, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] AutomatedAgents =
        ["bot", "crawl", "spider", "curl", "wget", "python", "httpclient", "java/", "go-http", "headless", "phantomjs", "scrapy", "okhttp", "axios", "node-fetch", "postman"];


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
