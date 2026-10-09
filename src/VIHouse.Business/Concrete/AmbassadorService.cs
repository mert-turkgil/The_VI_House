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
using VIHouse.Entities.Compliance;

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
    IRepository<ReferralWithdrawalRequest> withdrawals,
    IRepository<AmbassadorChannel> channels,
    IRepository<ConsentRecord> consents,
    IExperienceRepository experiences,
    ISeminarRepository seminars,
    ISeminarEnrollmentRepository seminarEnrollments,
    INotificationService notificationService,
    IEmailService emailService,
    IMediaStorage mediaStorage,
    StaffAlerts staffAlerts,
    IOptions<SiteOptions> siteOptions,
    IOptions<ReferralOptions> referralOptions,
    ILogger<AmbassadorService> logger,
    UserManager<ApplicationUser> userManager) : IAmbassadorService
{
    public long MinimumWithdrawalMinor => referralOptions.Value.MinimumWithdrawalMinor;

    public async Task RecordConversionAsync(string? referralCode, ReferralConversionKind kind, string sourceEntityType, Guid sourceEntityId,
        long? amountMinor = null, string? currency = null,
        ReferralTargetKind targetKind = ReferralTargetKind.Site, Guid? targetId = null,
        Guid? buyerUserId = null, string? buyerEmail = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(referralCode)) return;
        try
        {
            var ambassador = await ambassadors.GetByCodeAsync(referralCode.Trim(), ct);
            // Same rule as RecordVisitAsync: a switched-off influencer earns nothing new. The code
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
            var ambassadorUser = ambassador.UserId is { } ambassadorUserId ? await userManager.FindByIdAsync(ambassadorUserId.ToString()) : null;
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
                logger.LogWarning("Self-referral: influencer {AmbassadorId} bought through their own code ({SourceType} {SourceId}).",
                    ambassador.Id, sourceEntityType, sourceEntityId);

            var what = kind switch
            {
                ReferralConversionKind.Application => "Someone who came through your link has applied to an experience.",
                ReferralConversionKind.Approved => "An application that came through your link has been approved.",
                ReferralConversionKind.TicketPurchase => "Someone who came through your link has bought a ticket.",
                ReferralConversionKind.SessionPurchase => "Someone who came through your link has bought a place on a session.",
                _ => "Someone who came through your link has become a member.",
            };
            var amountText = amountMinor is { } a && currency is not null ? MoneyFormatter.Format(a, currency) : null;
            var commissionText = commission is { } c && currency is not null ? MoneyFormatter.Format(c, currency) : null;

            if (ambassador.UserId is { } notifyUserId)
                await notificationService.CreateForUserAsync(notifyUserId, NotificationType.ReferralConverted,
                "Your link just worked",
                amountText is null ? what : $"{what} {amountText}{(commissionText is null ? "" : $" — your commission {commissionText}")}.",
                SiteUrls.Influencer, ct);

            var user = ambassadorUser;
            if (user?.Email is not null)
            {
                await emailService.SendAsync("ReferralConverted", user.Email, "Your referral link just worked",
                    new ReferralConvertedEmailModel(ambassador.Name, kind, amountText, commissionText,
                        SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.InCulture(SiteUrls.Influencer, user.PreferredCulture))),
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


    public Task<List<Ambassador>> GetAllAsync(CancellationToken ct = default) => ambassadors.GetAllAsync(ct);

    public Task<Ambassador?> GetByIdAsync(Guid id, CancellationToken ct = default) => ambassadors.GetByIdAsync(id, ct);

    public Task<Ambassador?> GetByCodeAsync(string code, CancellationToken ct = default) => ambassadors.GetByCodeAsync(code, ct);

    public Task<Ambassador?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) => ambassadors.GetByUserIdAsync(userId, ct);

    public async Task<AmbassadorCreationResult> CreateForUserAsync(
        Guid userId, string name, string code, decimal commissionPercent, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await ambassadors.GetByCodeAsync(code, ct) is not null)
            return AmbassadorCreationResult.Fail("Influencer.Error.CodeTaken", code);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return AmbassadorCreationResult.Fail("Influencer.Error.AccountGone");
        if (await ambassadors.GetByUserIdAsync(userId, ct) is not null)
            return AmbassadorCreationResult.Fail("Influencer.Error.AlreadyInfluencer");

        if (!await userManager.IsInRoleAsync(user, Roles.Ambassador))
            await userManager.AddToRoleAsync(user, Roles.Ambassador);

        var ambassador = new Ambassador
        {
            UserId = user.Id,
            Code = code,
            Name = name,
            CommissionPercent = commissionPercent,
            Status = AmbassadorStatus.Active,
            ActivatedAt = DateTimeOffset.UtcNow,
            // A starting point for the legal name; Finance confirms it with the rest of the payout
            // details on the influencer's page.
            LegalFirstName = Text.NullIfBlank(user.FirstName),
            LegalLastName = Text.NullIfBlank(user.LastName),
        };
        await ambassadors.AddAsync(ambassador, ct);
        await LogAsync("AmbassadorCreated", ambassador.Id, adminUserId, ipAddress, null, new { ambassador.Code, ambassador.Name, ambassador.CommissionPercent }, ct);
        await ambassadors.SaveChangesAsync(ct);

        return AmbassadorCreationResult.Ok(ambassador, user.Id);
    }

    public async Task<AmbassadorCreationResult> InviteAsync(AmbassadorInvite invite, MediaUpload? photo, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var email = invite.Email.Trim();
        if (await ambassadors.GetByCodeAsync(invite.Code, ct) is not null)
            return AmbassadorCreationResult.Fail("Influencer.Error.CodeTaken", invite.Code);
        if (await userManager.FindByEmailAsync(email) is { } existing)
            return AmbassadorCreationResult.ExistingAccount(existing.Id);
        if (await ambassadors.GetPendingByInviteEmailAsync(email, ct) is { } pending)
            return AmbassadorCreationResult.Fail("Influencer.Error.PendingExists", email, pending.Code);
        if (InfluencerValidation.Payout(invite.Payout).Concat(InfluencerValidation.Profile(invite.Profile)).FirstOrDefault() is { } invalid)
            return AmbassadorCreationResult.Fail(invalid);

        var ambassador = new Ambassador
        {
            Code = invite.Code,
            Name = invite.Name,
            CommissionPercent = invite.CommissionPercent,
            Status = AmbassadorStatus.Pending,
            InviteEmail = email,
            PreferredCulture = SiteCultures.Normalise(invite.Culture),
        };
        await ApplyProfileAsync(ambassador, invite.Profile, ct);
        ApplyPayout(ambassador, invite.Payout);
        var token = NewInviteToken(ambassador);
        await ambassadors.AddAsync(ambassador, ct);
        await LogAsync("AmbassadorInvited", ambassador.Id, adminUserId, ipAddress, null,
            new { ambassador.Code, ambassador.Name, ambassador.CommissionPercent, ambassador.InviteEmail, ambassador.InviteExpiresAt }, ct);
        await ambassadors.SaveChangesAsync(ct);

        // The photo needs the row's id for its folder, so it follows the save. A refused file does
        // not cancel the invitation; the requirement checklist keeps asking for a photo.
        if (photo is not null) await SetPhotoAsync(ambassador.Id, photo, adminUserId, ipAddress, ct);

        var sent = await SendInviteAsync(ambassador, token, ct);
        return sent ? AmbassadorCreationResult.Ok(ambassador, null) : AmbassadorCreationResult.SavedButNotSent(ambassador);
    }

    public async Task<AmbassadorCreationResult> ResendInviteAsync(Guid ambassadorId, string? email, string? culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null || ambassador.Status != AmbassadorStatus.Pending)
            return AmbassadorCreationResult.Fail("Influencer.Error.OnlyPendingResend");

        var before = new { ambassador.InviteEmail, ambassador.PreferredCulture, ambassador.InviteExpiresAt };
        if (!string.IsNullOrWhiteSpace(email) && !string.Equals(email.Trim(), ambassador.InviteEmail, StringComparison.OrdinalIgnoreCase))
        {
            var corrected = email.Trim();
            if (await userManager.FindByEmailAsync(corrected) is { } existing)
                return AmbassadorCreationResult.ExistingAccount(existing.Id);
            if (await ambassadors.GetPendingByInviteEmailAsync(corrected, ct) is { } other && other.Id != ambassador.Id)
                return AmbassadorCreationResult.Fail("Influencer.Error.PendingExists", corrected, other.Code);
            ambassador.InviteEmail = corrected;
        }
        if (!string.IsNullOrWhiteSpace(culture)) ambassador.PreferredCulture = SiteCultures.Normalise(culture);

        var token = NewInviteToken(ambassador);
        ambassador.UpdatedAt = DateTimeOffset.UtcNow;
        await LogAsync("AmbassadorInviteResent", ambassador.Id, adminUserId, ipAddress, before,
            new { ambassador.InviteEmail, ambassador.PreferredCulture, ambassador.InviteExpiresAt }, ct);
        await ambassadors.SaveChangesAsync(ct);

        var sent = await SendInviteAsync(ambassador, token, ct);
        return sent ? AmbassadorCreationResult.Ok(ambassador, null) : AmbassadorCreationResult.SavedButNotSent(ambassador);
    }

    public async Task<bool> WithdrawInviteAsync(Guid ambassadorId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null || ambassador.Status != AmbassadorStatus.Pending) return false;

        // A pending row has never had a visit, a conversion or a payout (all need Active), so
        // removing it removes nothing but the reservation — and the photo the admin uploaded.
        var photoKey = ambassador.PhotoStorageKey;
        await LogAsync("AmbassadorInviteWithdrawn", ambassador.Id, adminUserId, ipAddress,
            new { ambassador.Code, ambassador.Name, ambassador.InviteEmail }, null, ct);
        ambassadors.Remove(ambassador);
        await ambassadors.SaveChangesAsync(ct);
        if (photoKey is not null) await mediaStorage.DeleteAsync(photoKey, ct);
        return true;
    }

    public async Task<AmbassadorInviteLookup> GetInviteAsync(string token, CancellationToken ct = default)
    {
        var ambassador = await FindByTokenAsync(token, ct);
        if (ambassador is null || ambassador.Status != AmbassadorStatus.Pending)
            return new AmbassadorInviteLookup(AmbassadorInviteState.Invalid, null, null, false);
        if (ambassador.InviteExpiresAt is not { } expires || expires < DateTimeOffset.UtcNow)
            return new AmbassadorInviteLookup(AmbassadorInviteState.Expired, ambassador, null, false);

        var account = await userManager.FindByEmailAsync(ambassador.InviteEmail!);
        return new AmbassadorInviteLookup(AmbassadorInviteState.Valid, ambassador, account?.Id,
            account is not null && await userManager.HasPasswordAsync(account));
    }

    public async Task<AmbassadorAcceptResult> AcceptInviteAsync(string token, AmbassadorAcceptance form, Guid? signedInUserId, string? ipAddress, CancellationToken ct = default)
    {
        var lookup = await GetInviteAsync(token, ct);
        if (lookup.State == AmbassadorInviteState.Invalid) return AmbassadorAcceptResult.Of(AmbassadorAcceptStatus.Invalid);
        if (lookup.State == AmbassadorInviteState.Expired) return AmbassadorAcceptResult.Of(AmbassadorAcceptStatus.Expired);
        var ambassador = lookup.Ambassador!;

        if (!form.AcceptedTerms || string.IsNullOrWhiteSpace(form.TermsText)) return AmbassadorAcceptResult.Reject("Terms");

        var user = lookup.AccountId is { } accountId ? await userManager.FindByIdAsync(accountId.ToString()) : null;
        if (user is not null && lookup.AccountHasPassword)
        {
            // The address already has a login. Holding the link proves the mailbox, but the account
            // is theirs to open, not the link's: they sign in with it first.
            if (signedInUserId is null) return AmbassadorAcceptResult.Of(AmbassadorAcceptStatus.SignInRequired);
            if (signedInUserId != user.Id) return AmbassadorAcceptResult.Of(AmbassadorAcceptStatus.WrongAccount);
        }
        else if (signedInUserId is { } someoneElse && someoneElse != user?.Id)
        {
            return AmbassadorAcceptResult.Of(AmbassadorAcceptStatus.WrongAccount);
        }
        else if (string.IsNullOrEmpty(form.Password))
        {
            return AmbassadorAcceptResult.Reject("Password");
        }

        // The account is named after the legal name the House entered; the display name is the
        // fallback for an invitation made before legal names were asked for.
        var firstName = ambassador.LegalFirstName ?? ambassador.Name;
        var lastName = ambassador.LegalLastName ?? "";

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = ambassador.InviteEmail,
                Email = ambassador.InviteEmail,
                // The only way here is the link sent to this address.
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                Country = ambassador.BillingCountry ?? "",
                MemberStatus = Entities.Users.MemberStatus.Active,
                PreferredCulture = ambassador.PreferredCulture,
            };
            var created = await userManager.CreateAsync(user, form.Password!);
            if (!created.Succeeded) return AmbassadorAcceptResult.Reject([.. created.Errors.Select(e => e.Description)]);
        }
        else
        {
            if (!lookup.AccountHasPassword)
            {
                var added = await userManager.AddPasswordAsync(user, form.Password!);
                if (!added.Succeeded) return AmbassadorAcceptResult.Reject([.. added.Errors.Select(e => e.Description)]);
            }
            user.EmailConfirmed = true;
            if (string.IsNullOrWhiteSpace(user.FirstName)) user.FirstName = firstName;
            if (string.IsNullOrWhiteSpace(user.LastName)) user.LastName = lastName;
            if (string.IsNullOrWhiteSpace(user.Country)) user.Country = ambassador.BillingCountry ?? "";
            user.PreferredCulture ??= ambassador.PreferredCulture;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, Roles.Ambassador))
            await userManager.AddToRoleAsync(user, Roles.Ambassador);

        var now = DateTimeOffset.UtcNow;
        var consent = await RecordTermsAsync(ambassador, user.Id, form.TermsText, ipAddress, now, ct);

        ambassador.UserId = user.Id;
        ambassador.Status = AmbassadorStatus.Active;
        ambassador.ActivatedAt = now;
        ambassador.InviteTokenHash = null;
        ambassador.UpdatedAt = now;

        // Recorded against the influencer's own account — they are the one acting here.
        await LogAsync("AmbassadorActivated", ambassador.Id, user.Id, ipAddress, null,
            new { ambassador.Code, UserId = user.Id, ambassador.TermsVersion, ambassador.TermsCommissionPercent, ConsentId = consent.Id }, ct);
        await ambassadors.SaveChangesAsync(ct);

        return new AmbassadorAcceptResult(AmbassadorAcceptStatus.Activated, user.Id, []);
    }

    public async Task<bool> AcceptTermsAsync(Guid ambassadorId, Guid userId, string termsText, string? ipAddress, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null || ambassador.UserId != userId || ambassador.TermsAcceptedAt is not null
            || string.IsNullOrWhiteSpace(termsText)) return false;

        var consent = await RecordTermsAsync(ambassador, userId, termsText, ipAddress, DateTimeOffset.UtcNow, ct);
        await LogAsync("AmbassadorTermsAccepted", ambassador.Id, userId, ipAddress, null,
            new { ambassador.TermsVersion, ambassador.TermsCommissionPercent, ConsentId = consent.Id }, ct);
        await ambassadors.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>The ConsentRecord holding the exact wording shown, and the terms stamp on the row.
    /// Saved by the caller.</summary>
    private async Task<ConsentRecord> RecordTermsAsync(Ambassador ambassador, Guid userId, string termsText, string? ipAddress, DateTimeOffset now, CancellationToken ct)
    {
        var consent = new ConsentRecord
        {
            UserId = userId,
            Type = ConsentType.AmbassadorTerms,
            Granted = true,
            Text = $"[{AmbassadorTerms.Version}] {termsText}",
            GrantedAt = now,
            IpAddress = ipAddress,
        };
        await consents.AddAsync(consent, ct);

        ambassador.TermsVersion = AmbassadorTerms.Version;
        ambassador.TermsAcceptedAt = now;
        ambassador.TermsConsentId = consent.Id;
        ambassador.TermsCommissionPercent = ambassador.CommissionPercent;
        ambassador.UpdatedAt = now;
        return consent;
    }

    /// <summary>32 random bytes, URL-safe. Only the hash is kept; a new token replaces the old one.</summary>
    private static string NewInviteToken(Ambassador ambassador)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        ambassador.InviteTokenHash = Text.Sha256Hex(token);
        ambassador.InviteSentAt = DateTimeOffset.UtcNow;
        ambassador.InviteExpiresAt = DateTimeOffset.UtcNow + IAmbassadorService.InviteLifetime;
        return token;
    }

    private Task<Ambassador?> FindByTokenAsync(string token, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(token) || token.Length > 100
            ? Task.FromResult<Ambassador?>(null)
            : ambassadors.GetByInviteTokenHashAsync(Text.Sha256Hex(token), ct);

    private async Task<bool> SendInviteAsync(Ambassador ambassador, string token, CancellationToken ct)
    {
        var culture = ambassador.PreferredCulture ?? SiteCultures.Default;
        var url = SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.InCulture(SiteUrls.InfluencerInvite(token), culture));
        return await emailService.SendAsync("AmbassadorInvite", ambassador.InviteEmail!, "You're invited to be a VI House influencer",
            new AmbassadorInviteEmailModel(ambassador.Name, url, ambassador.Code, ambassador.CommissionPercent, ambassador.InviteExpiresAt!.Value),
            culture, nameof(Ambassador), ambassador.Id, ct);
    }

    public async Task UpdateAsync(Ambassador updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await ambassadors.GetByIdAsync(updated.Id, ct)
            ?? throw new InvalidOperationException($"Ambassador {updated.Id} not found.");

        var before = new { existing.Name, existing.CommissionPercent, existing.Status };

        existing.Name = updated.Name;
        existing.CommissionPercent = updated.CommissionPercent;
        // Pending only ends by the invitee accepting (AcceptInviteAsync); an admin cannot switch a
        // pending row on, nor send an accepted one back to pending.
        if (existing.Status != AmbassadorStatus.Pending && updated.Status != AmbassadorStatus.Pending)
            existing.Status = updated.Status;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
        // Code is deliberately immutable after creation — changing it would silently orphan every
        // /r/{code} link already handed out.

        await LogAsync("AmbassadorUpdated", existing.Id, adminUserId, ipAddress,
            before, new { existing.Name, existing.CommissionPercent, existing.Status }, ct);
        await ambassadors.SaveChangesAsync(ct);
    }

    // --- Profile -----------------------------------------------------------------------------------

    public async Task<InfluencerSaveResult> UpdateProfileAsync(Guid ambassadorId, InfluencerProfileInput profile, Guid actorUserId, string? ipAddress, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null) return InfluencerSaveResult.Fail("Influencer.Error.NotFound");
        if (InfluencerValidation.Profile(profile) is { Count: > 0 } errors) return InfluencerSaveResult.Fail([.. errors]);

        var before = ProfileSnapshot(ambassador);
        await ApplyProfileAsync(ambassador, profile, ct);
        ambassador.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("InfluencerProfileUpdated", ambassador.Id, actorUserId, ipAddress, before, ProfileSnapshot(ambassador), ct);
        await ambassadors.SaveChangesAsync(ct);
        return InfluencerSaveResult.Ok();
    }

    public async Task<InfluencerSaveResult> UpdatePayoutIdentityAsync(Guid ambassadorId, InfluencerPayoutInput payout, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null) return InfluencerSaveResult.Fail("Influencer.Error.NotFound");
        if (InfluencerValidation.Payout(payout) is { Count: > 0 } errors) return InfluencerSaveResult.Fail([.. errors]);

        // The IBAN is masked in the audit trail: the log is read by more people than Finance.
        var before = new
        {
            ambassador.LegalFirstName, ambassador.LegalLastName, ambassador.BillingAddressLine1, ambassador.BillingCity,
            ambassador.BillingPostalCode, ambassador.BillingCountry, ambassador.TaxId, ambassador.PayoutAccountHolder,
            Iban = Iban.Mask(ambassador.PayoutIban), ambassador.PayoutBic,
        };
        var bankBefore = (ambassador.PayoutAccountHolder, ambassador.PayoutIban, ambassador.PayoutBic);
        ApplyPayout(ambassador, payout);
        var bankChanged = bankBefore != (ambassador.PayoutAccountHolder, ambassador.PayoutIban, ambassador.PayoutBic);
        if (!bankChanged) ambassador.PayoutDetailsUpdatedAt = ambassador.PayoutDetailsUpdatedAt ?? DateTimeOffset.UtcNow;
        ambassador.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync(bankChanged ? "AmbassadorPayoutDetailsChanged" : "AmbassadorPayoutIdentityUpdated", ambassador.Id, adminUserId, ipAddress, before,
            new
            {
                ambassador.LegalFirstName, ambassador.LegalLastName, ambassador.BillingAddressLine1, ambassador.BillingCity,
                ambassador.BillingPostalCode, ambassador.BillingCountry, ambassador.TaxId, ambassador.PayoutAccountHolder,
                Iban = Iban.Mask(ambassador.PayoutIban), ambassador.PayoutBic,
            }, ct);
        await ambassadors.SaveChangesAsync(ct);
        return InfluencerSaveResult.Ok();
    }

    public async Task<InfluencerSaveResult> SetPhotoAsync(Guid ambassadorId, MediaUpload upload, Guid actorUserId, string? ipAddress, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null) return InfluencerSaveResult.Fail("Influencer.Error.NotFound");
        if (!InfluencerValidation.IsPhoto(upload.FileName, upload.Length)) return InfluencerSaveResult.Fail("Influencer.Error.Photo");

        var saved = await mediaStorage.SaveAsync(upload, $"influencers/{ambassador.Id:N}", ct);
        if (!saved.Success) return InfluencerSaveResult.Fail("Influencer.Error.Photo");

        var previous = ambassador.PhotoStorageKey;
        ambassador.PhotoStorageKey = saved.StorageKey;
        ambassador.UpdatedAt = DateTimeOffset.UtcNow;
        await LogAsync("InfluencerPhotoChanged", ambassador.Id, actorUserId, ipAddress,
            new { PhotoStorageKey = previous }, new { ambassador.PhotoStorageKey }, ct);
        await ambassadors.SaveChangesAsync(ct);

        if (previous is not null) await mediaStorage.DeleteAsync(previous, ct);
        return InfluencerSaveResult.Ok();
    }

    public async Task<InfluencerSaveResult> RemovePhotoAsync(Guid ambassadorId, Guid actorUserId, string? ipAddress, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null) return InfluencerSaveResult.Fail("Influencer.Error.NotFound");
        if (ambassador.PhotoStorageKey is not { } previous) return InfluencerSaveResult.Ok();

        ambassador.PhotoStorageKey = null;
        ambassador.UpdatedAt = DateTimeOffset.UtcNow;
        await LogAsync("InfluencerPhotoRemoved", ambassador.Id, actorUserId, ipAddress, new { PhotoStorageKey = previous }, null, ct);
        await ambassadors.SaveChangesAsync(ct);
        await mediaStorage.DeleteAsync(previous, ct);
        return InfluencerSaveResult.Ok();
    }

    public async Task<MediaFileInfo?> OpenPhotoAsync(Guid ambassadorId, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        // The key comes from the row, never from the request.
        return ambassador?.PhotoStorageKey is { } key ? await mediaStorage.GetAsync(key, ct) : null;
    }

    /// <summary>
    /// Bio, niche and the channels, replaced as a whole. The old channel rows are removed and the new
    /// ones added through their repository, not by swapping the navigation collection: a new row
    /// carries a client-generated Guid key, and EF finding it on an already-tracked influencer by
    /// graph discovery would take it for an existing row and issue an UPDATE that touches nothing —
    /// the same reason JournalService and SeminarService add child rows explicitly. On an influencer
    /// not yet saved (the invitation) the rows simply go in with it.
    /// </summary>
    private async Task ApplyProfileAsync(Ambassador ambassador, InfluencerProfileInput profile, CancellationToken ct)
    {
        ambassador.Bio = Text.NullIfBlank(profile.Bio?.Trim());
        ambassador.Niche = Text.NullIfBlank(profile.Niche?.Trim());

        foreach (var old in ambassador.Channels.ToList())
        {
            channels.Remove(old);
            ambassador.Channels.Remove(old);
        }

        var order = 0;
        foreach (var channel in profile.Channels)
        {
            var row = new AmbassadorChannel
            {
                AmbassadorId = ambassador.Id,
                Platform = channel.Platform,
                Url = channel.Url.Trim(),
                Audience = channel.Audience,
                SortOrder = ++order,
            };
            await channels.AddAsync(row, ct);
            ambassador.Channels.Add(row);
        }
    }

    private static object ProfileSnapshot(Ambassador a) =>
        new { a.Bio, a.Niche, Channels = a.Channels.Select(c => new { c.Platform, c.Url, c.Audience }).ToList() };

    private static void ApplyPayout(Ambassador ambassador, InfluencerPayoutInput payout)
    {
        ambassador.LegalFirstName = payout.LegalFirstName.Trim();
        ambassador.LegalLastName = payout.LegalLastName.Trim();
        ambassador.BillingAddressLine1 = payout.AddressLine1.Trim();
        ambassador.BillingAddressLine2 = Text.NullIfBlank(payout.AddressLine2?.Trim());
        ambassador.BillingCity = payout.City.Trim();
        ambassador.BillingPostalCode = payout.PostalCode.Trim();
        ambassador.BillingCountry = payout.Country.Trim().ToUpperInvariant();
        ambassador.TaxId = Text.NullIfBlank(payout.TaxId?.Trim());

        var holder = payout.AccountHolder.Trim();
        var iban = Iban.Normalize(payout.Iban);
        var bic = string.IsNullOrWhiteSpace(payout.Bic) ? null : Iban.Normalize(payout.Bic);
        if (holder != ambassador.PayoutAccountHolder || iban != ambassador.PayoutIban || bic != ambassador.PayoutBic)
        {
            ambassador.PayoutAccountHolder = holder;
            ambassador.PayoutIban = iban;
            ambassador.PayoutBic = bic;
            ambassador.PayoutDetailsUpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    // --- Links and visits ----------------------------------------------------------------------------

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
            LandingPath = Text.Clip(landingPath, 200),
            UtmSource = Clip(utmSource),
            UtmMedium = Clip(utmMedium),
            UtmCampaign = Clip(utmCampaign),
            UtmContent = Clip(utmContent),
            IpHash = visitor?.IpHash,
            UserAgent = Text.Clip(Text.NullIfBlank(visitor?.UserAgent), 300),
            VisitorUserId = visitor?.UserId,
        }, ct);
        await visits.SaveChangesAsync(ct);
        return true;

        // The columns are 100 wide and the values come straight off a query string.
        static string? Clip(string? value) => Text.Clip(Text.NullIfBlank(value), 100);
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
                SeminarContent.Title(s, SiteCultures.Default), s.IsOnline ? null : s.Location, s.StartAtUtc));
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
            Balances = await BalancesAsync(ambassadorId, ledger, ct),
        };
    }

    /// <summary>Earned (net ledger commission) against paid (payouts), per currency.</summary>
    private async Task<List<CommissionBalance>> BalancesAsync(Guid ambassadorId, List<ReferralConversion> ledger, CancellationToken ct)
    {
        var earned = ledger
            .Where(c => c.Currency is not null && c.CommissionMinor is not null)
            .GroupBy(c => c.Currency!)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.NetCommissionMinor));
        var paid = (await payouts.FindAsync(p => p.AmbassadorId == ambassadorId, ct))
            .GroupBy(p => p.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountMinor));
        return earned.Keys.Union(paid.Keys)
            .OrderBy(c => c)
            .Select(c => new CommissionBalance(c, earned.GetValueOrDefault(c), paid.GetValueOrDefault(c)))
            .ToList();
    }

    /// <summary>What is owed in one currency right now.</summary>
    private async Task<long> OwedAsync(Guid ambassadorId, string currency, CancellationToken ct)
    {
        var earned = (await conversions.FindAsync(c => c.AmbassadorId == ambassadorId && c.Currency == currency && c.CommissionMinor != null, ct))
            .Sum(c => c.NetCommissionMinor);
        var paid = (await payouts.FindAsync(p => p.AmbassadorId == ambassadorId && p.Currency == currency, ct)).Sum(p => p.AmountMinor);
        return earned - paid;
    }

    /// <summary>
    /// Per link: which post brought the visits, and what those visits turned into. Visits are
    /// grouped by the target recorded at /r/{code}/…, conversions by the target on the ledger row —
    /// so a ticket bought after an experience-scoped link counts under that experience even if the
    /// buyer wandered around the site first. The title is null for the site link and for an
    /// experience or session that is no longer listed; the page names those in its own language.
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
            string? title = null;
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

            targets.Add(new ReferralTargetStats(kind, id, title, slug,
                Visits: allVisits.Count(v => v.TargetKind == kind && v.TargetId == id),
                Applications: ledger.Count(c => c.Kind == ReferralConversionKind.Application && c.TargetKind == kind && c.TargetId == id),
                Purchases: ledger.Count(c => c.TargetKind == kind && c.TargetId == id && c.ReversedAt is null
                    && c.Kind is ReferralConversionKind.TicketPurchase or ReferralConversionKind.MembershipPurchase or ReferralConversionKind.SessionPurchase)));
        }

        return targets.OrderByDescending(t => t.Purchases).ThenByDescending(t => t.Applications).ThenByDescending(t => t.Visits).ToList();
    }

    // --- The ledger ----------------------------------------------------------------------------------

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
                if (ambassador?.UserId is { } ambassadorUserId && takenBack > 0 && line.Currency is not null)
                {
                    await notificationService.CreateForUserAsync(ambassadorUserId, NotificationType.ReferralConverted,
                        "A referred purchase was refunded",
                        $"{(refunded >= amount ? "A purchase" : "Part of a purchase")} made through your link was refunded, so {MoneyFormatter.Format(takenBack, line.Currency)} of commission no longer stands.",
                        SiteUrls.InfluencerEarnings, ct);
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
        line.VoidReason = Text.Clip(reason, 300);
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
        if (ambassador is null) return ReferralPayoutResult.Fail("Influencer.Error.NotFound");

        var ledger = await conversions.FindAsync(c => c.AmbassadorId == ambassadorId && c.Currency == currency && c.CommissionMinor != null, ct);
        var earned = ledger.Sum(c => c.NetCommissionMinor);
        var paid = (await payouts.FindAsync(p => p.AmbassadorId == ambassadorId && p.Currency == currency, ct)).Sum(p => p.AmountMinor);
        var owed = earned - paid;

        if (owed != expectedOwedMinor)
            return ReferralPayoutResult.Fail("Influencer.Error.BalanceChanged", MoneyFormatter.Format(owed, currency));
        if (owed <= 0)
            return ReferralPayoutResult.Fail("Influencer.Error.NothingOwed", currency);

        var now = DateTimeOffset.UtcNow;
        var payout = new ReferralPayout
        {
            AmbassadorId = ambassadorId,
            Currency = currency,
            AmountMinor = owed,
            PaidAt = now,
            PaidByAdminId = adminUserId,
            Reference = Text.Clip(Text.NullIfBlank(reference), 100),
            Note = Text.Clip(Text.NullIfBlank(note), 500),
        };
        await payouts.AddAsync(payout, ct);
        foreach (var line in ledger.Where(c => c.PayoutId is null))
        {
            line.PayoutId = payout.Id;
            line.UpdatedAt = now;
        }

        // The payout answers an open withdrawal request in this currency, whichever button paid it.
        foreach (var request in await withdrawals.FindAsync(r => r.AmbassadorId == ambassadorId && r.Currency == currency && r.Status == WithdrawalStatus.Open, ct))
        {
            request.Status = WithdrawalStatus.Paid;
            request.PayoutId = payout.Id;
            request.DecidedAt = now;
            request.DecidedByAdminId = adminUserId;
            request.DecisionNote = payout.Note;
            request.UpdatedAt = now;
        }

        await LogAsync("ReferralCommissionPaid", ambassadorId, adminUserId, ipAddress, new { Owed = owed, Currency = currency },
            new { PayoutId = payout.Id, payout.AmountMinor, payout.Currency, payout.Reference }, ct);
        await payouts.SaveChangesAsync(ct);

        await TellInfluencerAsync(ambassador, "Commission paid",
            $"The House has paid you {MoneyFormatter.Format(owed, currency)} in commission{(payout.Reference is null ? "" : $" (reference {payout.Reference})")}.",
            "WithdrawalPaid", "Your commission has been paid",
            (name, url) => new WithdrawalPaidEmailModel(name, MoneyFormatter.Format(owed, currency), payout.Reference, url), ct);
        return ReferralPayoutResult.Ok(payout);
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
            OwnVisits: ambassador?.UserId is not { } ownUserId ? 0 : recent.Count(v => v.VisitorUserId == ownUserId),
            SelfReferrals: selfReferrals);
    }

    /// <summary>No User-Agent at all, or one a script, crawler or headless browser sends.</summary>
    private static bool LooksAutomated(string? userAgent) =>
        string.IsNullOrWhiteSpace(userAgent) || userAgent.Length < 10
        || AutomatedAgents.Any(a => userAgent.Contains(a, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] AutomatedAgents =
        ["bot", "crawl", "spider", "curl", "wget", "python", "httpclient", "java/", "go-http", "headless", "phantomjs", "scrapy", "okhttp", "axios", "node-fetch", "postman"];

    // --- Withdrawals ---------------------------------------------------------------------------------

    public async Task<WithdrawalResult> RequestWithdrawalAsync(Guid ambassadorId, string currency, string? note, Guid userId, string? ipAddress, CancellationToken ct = default)
    {
        currency = (currency ?? "").Trim().ToUpperInvariant();
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        if (ambassador is null || ambassador.UserId != userId) return WithdrawalResult.Fail("Influencer.Error.NotFound");
        if (ambassador.MissingRequirements().Count > 0) return WithdrawalResult.Fail("Influencer.Error.ProfileIncomplete");

        var owed = await OwedAsync(ambassadorId, currency, ct);
        if (owed < MinimumWithdrawalMinor)
            return WithdrawalResult.Fail("Influencer.Error.BelowMinimum", MoneyFormatter.Format(MinimumWithdrawalMinor, currency));
        if (await withdrawals.CountAsync(r => r.AmbassadorId == ambassadorId && r.Currency == currency && r.Status == WithdrawalStatus.Open, ct) > 0)
            return WithdrawalResult.Fail("Influencer.Error.AlreadyRequested");

        var request = new ReferralWithdrawalRequest
        {
            AmbassadorId = ambassadorId,
            Currency = currency,
            RequestedMinor = owed,
            Note = Text.Clip(Text.NullIfBlank(note?.Trim()), 500),
            RequestedAt = DateTimeOffset.UtcNow,
        };
        await withdrawals.AddAsync(request, ct);
        await LogAsync("InfluencerWithdrawalRequested", ambassadorId, userId, ipAddress, null,
            new { RequestId = request.Id, request.Currency, request.RequestedMinor }, ct);
        await withdrawals.SaveChangesAsync(ct);

        var amount = MoneyFormatter.Format(owed, currency);
        await staffAlerts.SendAsync(Roles.PayoutApprovers,
            "Withdrawal requested", $"{ambassador.Name} asked to be paid {amount}.", "/admin/withdrawals",
            "WithdrawalRequested", "An influencer asked to be paid",
            link => new WithdrawalRequestedEmailModel(ambassador.Name, amount, request.Note, link),
            nameof(ReferralWithdrawalRequest), request.Id, ct);

        return WithdrawalResult.Ok(request);
    }

    public async Task<bool> CancelWithdrawalAsync(Guid ambassadorId, Guid requestId, Guid userId, string? ipAddress, CancellationToken ct = default)
    {
        var ambassador = await ambassadors.GetByIdAsync(ambassadorId, ct);
        var request = await withdrawals.GetByIdAsync(requestId, ct);
        if (ambassador is null || ambassador.UserId != userId || request is null
            || request.AmbassadorId != ambassadorId || request.Status != WithdrawalStatus.Open) return false;

        request.Status = WithdrawalStatus.Cancelled;
        request.DecidedAt = DateTimeOffset.UtcNow;
        request.UpdatedAt = DateTimeOffset.UtcNow;
        await LogAsync("InfluencerWithdrawalCancelled", ambassadorId, userId, ipAddress, null, new { RequestId = request.Id }, ct);
        await withdrawals.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<ReferralWithdrawalRequest>> GetWithdrawalsAsync(Guid ambassadorId, CancellationToken ct = default) =>
        (await withdrawals.FindAsync(r => r.AmbassadorId == ambassadorId, ct)).OrderByDescending(r => r.RequestedAt).ToList();

    public async Task<List<WithdrawalQueueItem>> GetWithdrawalQueueAsync(CancellationToken ct = default)
    {
        var all = await withdrawals.GetAllAsync(ct);
        var byId = (await ambassadors.GetAllAsync(ct)).ToDictionary(a => a.Id);
        var items = new List<WithdrawalQueueItem>();
        foreach (var request in all)
        {
            if (!byId.TryGetValue(request.AmbassadorId, out var ambassador)) continue;
            var owedNow = request.Status == WithdrawalStatus.Open ? await OwedAsync(request.AmbassadorId, request.Currency, ct) : 0;
            items.Add(new WithdrawalQueueItem(request, ambassador, owedNow));
        }

        // Open requests oldest first (the queue); decided ones after, most recent first (the record).
        return items.Where(i => i.Request.Status == WithdrawalStatus.Open).OrderBy(i => i.Request.RequestedAt)
            .Concat(items.Where(i => i.Request.Status != WithdrawalStatus.Open).OrderByDescending(i => i.Request.DecidedAt ?? i.Request.RequestedAt))
            .ToList();
    }

    public Task<int> CountOpenWithdrawalsAsync(CancellationToken ct = default) =>
        withdrawals.CountAsync(r => r.Status == WithdrawalStatus.Open, ct);

    public async Task<ReferralPayoutResult> PayWithdrawalAsync(Guid requestId, long expectedOwedMinor, string? reference, string? note, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var request = await withdrawals.GetByIdAsync(requestId, ct);
        if (request is null || request.Status != WithdrawalStatus.Open) return ReferralPayoutResult.Fail("Influencer.Error.RequestNotOpen");

        // The payout settles the request (see MarkCommissionPaidAsync), so there is one way money is
        // recorded as paid, with one stale-balance guard.
        return await MarkCommissionPaidAsync(request.AmbassadorId, request.Currency, expectedOwedMinor, reference, note, adminUserId, ipAddress, ct);
    }

    public async Task<bool> RejectWithdrawalAsync(Guid requestId, string reason, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var request = await withdrawals.GetByIdAsync(requestId, ct);
        if (request is null || request.Status != WithdrawalStatus.Open || string.IsNullOrWhiteSpace(reason)) return false;
        var ambassador = await ambassadors.GetByIdAsync(request.AmbassadorId, ct);
        if (ambassador is null) return false;

        request.Status = WithdrawalStatus.Rejected;
        request.DecidedAt = DateTimeOffset.UtcNow;
        request.DecidedByAdminId = adminUserId;
        request.DecisionNote = Text.Clip(reason.Trim(), 500);
        request.UpdatedAt = DateTimeOffset.UtcNow;
        await LogAsync("InfluencerWithdrawalRejected", ambassador.Id, adminUserId, ipAddress, null,
            new { RequestId = request.Id, request.DecisionNote }, ct);
        await withdrawals.SaveChangesAsync(ct);

        var amount = MoneyFormatter.Format(request.RequestedMinor, request.Currency);
        await TellInfluencerAsync(ambassador, "Withdrawal request declined",
            $"Your request to be paid {amount} was declined: {request.DecisionNote}",
            "WithdrawalRejected", "About your withdrawal request",
            (name, url) => new WithdrawalRejectedEmailModel(name, amount, request.DecisionNote!, url), ct);
        return true;
    }

    /// <summary>A bell notification and an email (in their language) to the influencer, linking to
    /// their earnings page. Never throws.</summary>
    private async Task TellInfluencerAsync<TModel>(Ambassador ambassador, string title, string body,
        string emailTemplate, string emailSubject, Func<string, string, TModel> model, CancellationToken ct)
    {
        if (ambassador.UserId is not { } userId) return;
        try
        {
            await notificationService.CreateForUserAsync(userId, NotificationType.Influencer, title, body, SiteUrls.InfluencerEarnings, ct);
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user?.Email is null) return;
            var url = SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.InCulture(SiteUrls.InfluencerEarnings, user.PreferredCulture));
            await emailService.SendAsync(emailTemplate, user.Email, emailSubject, model(ambassador.Name, url),
                user.PreferredCulture ?? SiteCultures.Default, nameof(Ambassador), ambassador.Id, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not tell influencer {AmbassadorId} about {Template}.", ambassador.Id, emailTemplate);
        }
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
