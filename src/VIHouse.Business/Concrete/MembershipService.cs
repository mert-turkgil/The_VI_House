using VIHouse.Business;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Compliance;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Notifications;
using VIHouse.Entities.Users;

using VIHouse.Entities.Referrals;

namespace VIHouse.Business.Concrete;

public class MembershipService(
    IRepository<MembershipPlan> plans,
    IRepository<Membership> memberships,
    IMembershipPaymentRepository membershipPayments,
    IPendingJoinRepository pendingJoins,
    IPromoCodeRepository promoCodes,
    IProfileRepository profiles,
    IRepository<ConsentRecord> consentRecords,
    IPaymentProvider paymentProvider,
    IPaymentCatalogProvider catalog,
    IEmailService emailService,
    INotificationService notificationService,
    IAuditLogRepository auditLogs,
    IAmbassadorService ambassadorService,
    IPaymentTransactionService transactions,
    IOutbox outbox,
    IOptions<SiteOptions> siteOptions,
    ISiteSettingsService siteSettings,
    UserManager<ApplicationUser> userManager,
    ILogger<MembershipService> logger) : IMembershipService
{
    // =============================================================================================
    // Plans
    // =============================================================================================

    public async Task<List<MembershipPlan>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var active = await plans.FindAsync(p => p.Status == MembershipPlanStatus.Active, ct);
        return active.OrderBy(p => p.SortOrder).ThenBy(p => p.PriceMinor).ToList();
    }

    public async Task<List<MembershipPlan>> GetAllPlansAsync(CancellationToken ct = default) =>
        (await plans.GetAllAsync(ct)).OrderBy(p => p.SortOrder).ThenBy(p => p.PriceMinor).ToList();

    public Task<MembershipPlan?> GetPlanAsync(Guid id, CancellationToken ct = default) => plans.GetByIdAsync(id, ct);

    public async Task<MembershipPlan> CreatePlanAsync(MembershipPlan plan, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        await plans.AddAsync(plan, ct);
        await LogAsync("MembershipPlanCreated", plan.Id, adminUserId, ipAddress,
            before: null, after: Snapshot(plan), ct);
        await plans.SaveChangesAsync(ct);

        // Saved locally first, then mirrored: the row must exist before its id can ride along as
        // provider metadata, and a provider failure must not undo the admin's work.
        await PushToProviderAsync(plan, ct);
        await plans.SaveChangesAsync(ct);
        return plan;
    }

    public async Task UpdatePlanAsync(MembershipPlan updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await plans.GetByIdAsync(updated.Id, ct)
            ?? throw new InvalidOperationException($"Membership plan {updated.Id} not found.");

        var before = Snapshot(existing);

        existing.Name = updated.Name;
        existing.Description = updated.Description;
        existing.PriceMinor = updated.PriceMinor;
        existing.Currency = updated.Currency;
        existing.BillingPeriod = updated.BillingPeriod;
        existing.Features = updated.Features;
        existing.Status = updated.Status;
        existing.SortOrder = updated.SortOrder;
        existing.MaxMembers = updated.MaxMembers;
        existing.IncludesCommunity = updated.IncludesCommunity;
        existing.IncludesSessions = updated.IncludesSessions;
        existing.IncludesDirectory = updated.IncludesDirectory;
        existing.IncludesMemberCard = updated.IncludesMemberCard;
        existing.DiscordRoleId = updated.DiscordRoleId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("MembershipPlanUpdated", existing.Id, adminUserId, ipAddress, before, Snapshot(existing), ct);

        // No explicit Update() call: `existing` is already tracked, loaded on this same scoped
        // DbContext — same reasoning as ExperienceService.UpdateCoreFieldsAsync.
        await plans.SaveChangesAsync(ct);

        await PushToProviderAsync(existing, ct);
        await plans.SaveChangesAsync(ct);
    }

    public async Task ArchivePlanAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var plan = await plans.GetByIdAsync(id, ct);
        if (plan is null) return;

        plan.Status = MembershipPlanStatus.Archived;
        plan.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("MembershipPlanArchived", id, adminUserId, ipAddress, before: null, after: new { plan.Name }, ct);
        await plans.SaveChangesAsync(ct);

        await PushToProviderAsync(plan, ct);
        await plans.SaveChangesAsync(ct);
    }

    public async Task<PlanMutationResult> DeletePlanAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var plan = await plans.GetByIdAsync(id, ct);
        if (plan is null) return PlanMutationResult.Fail("That plan no longer exists.");

        var usage = await GetPlanUsageAsync(id, ct);
        if (!usage.CanDelete)
        {
            return PlanMutationResult.Fail(
                $"\"{plan.Name}\" has {usage.Memberships} membership(s) and {usage.Payments} payment(s) against it, so it can't be deleted — archive it instead.");
        }

        // The provider side first, while the ids are still to hand. A failure here is reported
        // rather than swallowed: leaving a live product on sale at Stripe for a plan that no longer
        // exists is precisely the drift the catalogue exists to prevent.
        if (plan.ProviderProductId is not null)
        {
            try
            {
                await catalog.ArchivePlanAsync(plan.ProviderProductId, plan.ProviderPriceId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not archive plan {PlanId} at the payment provider before deleting it", id);
                return PlanMutationResult.Fail($"Stripe refused to archive the product ({ex.Message}). Nothing was deleted.");
            }
        }

        await LogAsync("MembershipPlanDeleted", id, adminUserId, ipAddress, before: Snapshot(plan), after: null, ct);
        plans.Remove(plan);
        await plans.SaveChangesAsync(ct);

        return PlanMutationResult.Ok($"\"{plan.Name}\" deleted." + (plan.ProviderProductId is null ? "" : " Its Stripe product has been archived."));
    }

    public async Task<PlanMutationResult> SyncPlanAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var plan = await plans.GetByIdAsync(id, ct);
        if (plan is null) return PlanMutationResult.Fail("That plan no longer exists.");

        var replaced = await PushToProviderAsync(plan, ct);
        await plans.SaveChangesAsync(ct);

        if (plan.ProviderSyncError is not null)
            return PlanMutationResult.Fail($"Sync failed: {plan.ProviderSyncError}");

        await LogAsync("MembershipPlanSynced", id, adminUserId, ipAddress, before: null,
            after: new { plan.ProviderProductId, plan.ProviderPriceId }, ct);
        await plans.SaveChangesAsync(ct);

        return PlanMutationResult.Ok(replaced
            ? $"\"{plan.Name}\" synced — the price changed, so a new Stripe price was issued and the old one archived."
            : $"\"{plan.Name}\" is in sync with Stripe.");
    }

    public async Task<PlanSyncSummary> SyncAllPlansAsync(Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var all = await plans.GetAllAsync(ct);
        var errors = new List<string>();
        var synced = 0;

        foreach (var plan in all.OrderBy(p => p.SortOrder))
        {
            await PushToProviderAsync(plan, ct);
            if (plan.ProviderSyncError is null) synced++;
            else errors.Add($"{plan.Name}: {plan.ProviderSyncError}");
        }

        await plans.SaveChangesAsync(ct);
        await LogAsync("MembershipPlansSyncedAll", Guid.Empty, adminUserId, ipAddress, before: null,
            after: new { Synced = synced, Failed = errors.Count }, ct);
        await plans.SaveChangesAsync(ct);

        return new PlanSyncSummary(synced, errors.Count, errors);
    }

    public async Task<PlanImportSummary> ImportPlansFromProviderAsync(Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        List<CatalogPlan> remote;
        try
        {
            remote = await catalog.ListPlansAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not list plans at the payment provider");
            return new PlanImportSummary(0, 0, ex.Message);
        }

        var local = await plans.GetAllAsync(ct);
        var imported = 0;
        var skipped = 0;

        foreach (var item in remote)
        {
            // Already mirrored — by product id, or by the plan id we stamped into its metadata
            // (which survives the local Provider* columns being cleared). Existing rows are never
            // touched by an import.
            var known = local.Any(p => p.ProviderProductId == item.ProductId)
                || (item.LocalPlanId is { } localId && local.Any(p => p.Id == localId));
            if (known)
            {
                skipped++;
                continue;
            }

            var plan = new MembershipPlan
            {
                Name = item.Name,
                Description = item.Description,
                PriceMinor = item.AmountMinor,
                Currency = item.Currency,
                BillingPeriod = item.Recurring switch
                {
                    RecurringInterval.Monthly => MembershipBillingPeriod.Monthly,
                    RecurringInterval.Annual => MembershipBillingPeriod.Annual,
                    _ => MembershipBillingPeriod.OneTime,
                },
                // Archived on arrival: the admin decides what goes on the public page, not whatever
                // happened to be active in the Stripe dashboard.
                Status = MembershipPlanStatus.Archived,
                SortOrder = local.Count + imported,
                ProviderProductId = item.ProductId,
                ProviderPriceId = item.PriceId,
                ProviderSyncedAt = DateTimeOffset.UtcNow,
            };

            await plans.AddAsync(plan, ct);
            await LogAsync("MembershipPlanImported", plan.Id, adminUserId, ipAddress, before: null, after: Snapshot(plan), ct);
            imported++;
        }

        await plans.SaveChangesAsync(ct);
        return new PlanImportSummary(imported, skipped, null);
    }

    public async Task<PlanUsage> GetPlanUsageAsync(Guid id, CancellationToken ct = default)
    {
        var membershipCount = (await memberships.FindAsync(m => m.PlanId == id, ct)).Count;
        var paymentCount = (await membershipPayments.FindAsync(p => p.PlanId == id, ct)).Count;
        return new PlanUsage(membershipCount, paymentCount);
    }

    /// <summary>
    /// The one place a plan reaches the provider. Records the outcome on the row — ids and a
    /// timestamp on success, the message on failure — and never throws, so every caller can save
    /// the local change regardless. Returns whether the provider issued a replacement price.
    /// </summary>
    private async Task<bool> PushToProviderAsync(MembershipPlan plan, CancellationToken ct)
    {
        try
        {
            if (plan.Status == MembershipPlanStatus.Archived)
            {
                // Retiring takes precedence over any pending edit: an archived plan should not be
                // buyable at Stripe whatever else changed. A plan that was never mirrored has
                // nothing to retire and is simply marked as up to date.
                if (plan.ProviderProductId is not null)
                    await catalog.ArchivePlanAsync(plan.ProviderProductId, plan.ProviderPriceId, ct);

                plan.ProviderSyncedAt = DateTimeOffset.UtcNow;
                plan.ProviderSyncError = null;
                return false;
            }

            var result = await catalog.SyncPlanAsync(new CatalogPlan(
                plan.ProviderProductId, plan.ProviderPriceId, plan.Id,
                plan.Name, PlanCheckoutDescription(plan), plan.PriceMinor, plan.Currency,
                ToRecurringInterval(plan.BillingPeriod), Active: true)
            {
                ImageUrl = await MembershipImageUrlAsync(ct),
            }, ct);

            plan.ProviderProductId = result.ProductId;
            plan.ProviderPriceId = result.PriceId;
            plan.ProviderSyncedAt = DateTimeOffset.UtcNow;
            plan.ProviderSyncError = null;
            return result.PriceReplaced;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not sync membership plan {PlanId} to the payment provider", plan.Id);
            plan.ProviderSyncedAt = null;
            plan.ProviderSyncError = Truncate(ex.Message, 1000);
            return false;
        }
    }

    // =============================================================================================
    // Membership
    // =============================================================================================

    /// <summary>
    /// The membership that currently entitles someone to anything, or null.
    ///
    /// This is the single entitlement primitive in the application — the member directory, the member
    /// card, the community links, members-only seminars and now joining an experience all resolve
    /// through it, so what it counts as "current" is worth being exact about.
    ///
    /// The ExpiresAt check is not redundant with the status. A cancellation webhook sets Cancelled,
    /// but nothing sweeps lapsed rows to Expired — so filtering on Status alone would let a
    /// membership that lapsed a year ago open every one of those doors, permanently. Expiry is
    /// therefore derived from the date rather than trusted to a column somebody has to remember to
    /// update.
    /// </summary>
    public Task<PlanAvailability> GetPlanAvailabilityAsync(Guid planId, CancellationToken ct = default) =>
        GetPlanAvailabilityAsync(planId, excludePendingJoinId: null, ct);

    /// <param name="excludePendingJoinId">A pending join that is being (re)opened right now — its
    /// own earlier session must not count against the person resuming it.</param>
    private async Task<PlanAvailability> GetPlanAvailabilityAsync(Guid planId, Guid? excludePendingJoinId, CancellationToken ct)
    {
        var plan = await plans.GetByIdAsync(planId, ct);
        if (plan?.MaxMembers is not { } max)
            return new PlanAvailability(null, 0);

        var now = DateTimeOffset.UtcNow;
        var dayAgo = now.AddDays(-1);

        var members = await memberships.CountAsync(m => m.PlanId == planId
            && (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.PastDue)
            && (m.ExpiresAt == null || m.ExpiresAt > now), ct);

        // Seats in checkout. A join whose provider session is still payable, and a signed-in
        // checkout opened recently — the provider's own session window is a day, so anything
        // older than that is abandoned, not pending.
        var inJoinCheckout = await pendingJoins.CountAsync(p => p.PlanId == planId
            && p.Status == PendingJoinStatus.Pending
            && p.ProviderSessionId != null && p.SessionExpiresAt > now
            && (excludePendingJoinId == null || p.Id != excludePendingJoinId), ct);

        var inMemberCheckout = await membershipPayments.CountAsync(mp => mp.PlanId == planId
            && mp.Status == PaymentStatus.Created && mp.CreatedAt > dayAgo, ct);

        return new PlanAvailability(max, members + inJoinCheckout + inMemberCheckout);
    }

    private const string PlanFullMessage = "This plan is full at the moment. Join the waitlist and we'll let you know the moment a place opens.";

    public async Task<PlanMutationResult> GrantComplimentaryAsync(Guid userId, Guid planId, DateTimeOffset? expiresAt, bool overrideCap, string? note, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return PlanMutationResult.Fail("That account no longer exists.");

        var plan = await plans.GetByIdAsync(planId, ct);
        if (plan is null || plan.Status != MembershipPlanStatus.Active)
            return PlanMutationResult.Fail("Choose an active plan.");

        if (await GetCurrentMembershipAsync(userId, ct) is not null)
            return PlanMutationResult.Fail("They already hold a current membership — revoke it first.");

        var now = DateTimeOffset.UtcNow;
        if (expiresAt is { } until && until <= now)
            return PlanMutationResult.Fail("The expiry date has to be in the future.");

        var availability = await GetPlanAvailabilityAsync(planId, ct);
        if (availability.IsFull && !overrideCap)
            return PlanMutationResult.Fail($"{plan.Name} is at its member limit ({availability.Taken}/{plan.MaxMembers}). Tick the override to grant a seat anyway.");

        expiresAt ??= plan.BillingPeriod switch
        {
            MembershipBillingPeriod.Monthly => now.AddMonths(1),
            MembershipBillingPeriod.Annual => now.AddYears(1),
            _ => null,
        };

        var membership = new Membership
        {
            UserId = userId,
            PlanId = planId,
            StartAt = now,
            // No RenewalAt: nothing renews a complimentary membership, it simply ends.
            ExpiresAt = expiresAt,
            Status = MembershipStatus.Active,
            IsComplimentary = true,
            GrantedByUserId = adminUserId,
            GrantNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        await memberships.AddAsync(membership, ct);
        await memberships.SaveChangesAsync(ct);

        if (!await userManager.IsInRoleAsync(user, Roles.Member))
            await userManager.AddToRoleAsync(user, Roles.Member);
        if (user.MemberStatus != MemberStatus.Active)
        {
            user.MemberStatus = MemberStatus.Active;
            await userManager.UpdateAsync(user);
        }

        await LogAsync("MembershipGranted", membership.Id, adminUserId, ipAddress, null,
            new { UserId = userId, PlanId = planId, plan.Name, ExpiresAt = expiresAt, Note = membership.GrantNote, OverrodeCap = availability.IsFull && overrideCap }, ct);
        if (availability.IsFull && overrideCap)
            await LogAsync("MembershipCapExceeded", planId, adminUserId, ipAddress, null,
                new { MembershipId = membership.Id, Taken = availability.Taken + 1, Max = plan.MaxMembers }, ct);
        await auditLogs.SaveChangesAsync(ct);

        if (user.Email is not null)
        {
            await emailService.SendAsync(
                "MembershipConfirmed", user.Email, $"Welcome — you're a {plan.Name}",
                new MembershipConfirmedEmailModel(user.FirstName, plan.Name, expiresAt)
                {
                    Status = MembershipEmailStatus.Granted,
                    MemberNumber = $"VIH-{membership.Id:N}"[..12].ToUpperInvariant(),
                    AccountUrl = SiteUrls.Absolute(BaseUrl, SiteUrls.AccountMembership),
                },
                nameof(Membership), membership.Id, ct);
        }

        await notificationService.CreateForUserAsync(userId, NotificationType.Payment,
            $"Welcome to {plan.Name}",
            expiresAt is { } e ? $"Your membership is active until {e:d MMMM yyyy}." : "Your membership is active.",
            SiteUrls.Account, ct);

        return PlanMutationResult.Ok(expiresAt is { } x
            ? $"{plan.Name} granted to {user.Email} until {x:d MMM yyyy}."
            : $"{plan.Name} granted to {user.Email} with no expiry.");
    }

    public async Task<PlanMutationResult> RevokeMembershipAsync(Guid userId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var membership = await GetCurrentMembershipAsync(userId, ct);
        if (membership is null) return PlanMutationResult.Fail("They have no current membership.");

        if (membership.ProviderSubscriptionId is not null && !membership.IsComplimentary)
            return PlanMutationResult.Fail("This membership is billed by Stripe. Cancel the subscription there (or have the member do it from their billing page) and it will end here automatically.");

        var now = DateTimeOffset.UtcNow;
        var before = new { membership.Status, membership.ExpiresAt };
        membership.Status = MembershipStatus.Cancelled;
        membership.CancelledAt = now;
        membership.ExpiresAt = now;
        membership.UpdatedAt = now;
        await memberships.SaveChangesAsync(ct);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is not null && user.MemberStatus == MemberStatus.Active)
        {
            user.MemberStatus = MemberStatus.Cancelled;
            await userManager.UpdateAsync(user);
        }

        if (user is not null)
        {
            var revokedPlan = await plans.GetByIdAsync(membership.PlanId, ct);
            await AnnounceMembershipEndedAsync(user, membership.Id, revokedPlan?.Name ?? "membership", now, wasRevoked: true, ct);
        }

        await LogAsync("MembershipRevoked", membership.Id, adminUserId, ipAddress, before,
            new { membership.Status, membership.ExpiresAt, UserId = userId }, ct);
        await auditLogs.SaveChangesAsync(ct);

        return PlanMutationResult.Ok("Membership ended.");
    }

    public async Task<Membership?> GetCurrentMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var mine = await memberships.FindAsync(m => m.UserId == userId, ct);

        return mine
            // PastDue counts: a declined renewal does not end the period already paid for. The
            // ExpiresAt filter below is what ends it.
            .Where(m => m.Status is MembershipStatus.Active or MembershipStatus.PastDue)
            // A null ExpiresAt is a one-time membership that does not lapse, not an expired one.
            .Where(m => m.ExpiresAt is null || m.ExpiresAt > now)
            .OrderByDescending(m => m.StartAt)
            .FirstOrDefault();
    }

    public async Task<MemberEntitlements?> GetEntitlementsAsync(Guid userId, CancellationToken ct = default) =>
        (await GetMembershipSummaryAsync(userId, ct))?.Entitlements;

    public async Task<MembershipSummary?> GetMembershipSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var membership = await GetCurrentMembershipAsync(userId, ct);
        if (membership is null) return null;

        var plan = await plans.GetByIdAsync(membership.PlanId, ct);
        return plan is null ? null : new MembershipSummary(membership, plan);
    }

    public async Task<List<MembershipSummary>> GetMembershipHistoryAsync(Guid userId, CancellationToken ct = default)
    {
        var mine = await memberships.FindAsync(m => m.UserId == userId, ct);
        if (mine.Count == 0) return [];

        var planIds = mine.Select(m => m.PlanId).Distinct().ToList();
        var known = (await plans.FindAsync(p => planIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);

        return mine
            .OrderByDescending(m => m.StartAt)
            .Where(m => known.ContainsKey(m.PlanId))
            .Select(m => new MembershipSummary(m, known[m.PlanId]))
            .ToList();
    }

    public async Task<string?> CreateBillingPortalUrlAsync(Guid userId, string returnUrl, CancellationToken ct = default)
    {
        var membership = await GetCurrentMembershipAsync(userId, ct);
        if (membership?.ProviderCustomerId is null) return null;

        return await paymentProvider.CreateBillingPortalUrlAsync(membership.ProviderCustomerId, returnUrl, ct);
    }

    public async Task<MembershipCheckoutResult> InitiateCheckoutAsync(Guid planId, Guid userId, string? referralCode, string? promoCode, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var plan = await plans.GetByIdAsync(planId, ct);
        if (plan is null || plan.Status != MembershipPlanStatus.Active)
            return MembershipCheckoutResult.Fail("This membership plan isn't available right now.");

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return MembershipCheckoutResult.Fail("Your account couldn't be found — please log in again.");

        // One current membership per person. Buying a second while the first still runs would
        // open two rows and, for a recurring plan, two subscriptions billing side by side.
        if (await GetCurrentMembershipAsync(userId, ct) is { } current)
        {
            return MembershipCheckoutResult.Fail(current.Status == MembershipStatus.PastDue
                ? "Your membership has a payment outstanding. Update your card from your account page and it will continue — there's no need to buy again."
                : "You already hold an active membership. To change plan, manage your billing from your account page or contact us.");
        }

        if ((await GetPlanAvailabilityAsync(plan.Id, ct)).IsFull)
            return MembershipCheckoutResult.Full(PlanFullMessage);

        var (promo, promoError) = await ResolveMembershipPromoAsync(promoCode, plan, user.Email!, ct);
        if (promoError is not null)
            return MembershipCheckoutResult.Fail(promoError);

        return await StartCheckoutAsync(plan, user, referralCode, promo, successUrl, cancelUrl, ct);
    }

    // ---------------------------------------------------------------------------------------------
    // Join (no account yet)
    // ---------------------------------------------------------------------------------------------

    private const string JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string TermsConsentText = "I agree to The VI House Terms & Conditions and Privacy Policy.";
    private const string AlreadyMemberMessage =
        "This email address already holds a membership. Look for the account setup email we sent you — it has the link to choose your password — or contact us and we'll resend it.";

    public async Task<MembershipCheckoutResult> InitiateJoinCheckoutAsync(JoinRequest request, string successUrl, string cancelUrlTemplate, CancellationToken ct = default)
    {
        var plan = await plans.GetByIdAsync(request.PlanId, ct);
        if (plan is null || plan.Status != MembershipPlanStatus.Active)
            return MembershipCheckoutResult.Fail("This membership plan isn't available right now.");

        var email = request.Email.Trim();
        var normalized = NormalizeEmail(email);

        // Refusing rather than reusing when the address belongs to someone who can sign in:
        // attaching this payment to their account would let anyone who knows a member's email buy
        // "for" them, and would hand the payer a setup link to an account that isn't theirs. An
        // account that *cannot* sign in — no password, no external login, no staff role — is a
        // ghost left by an earlier abandoned checkout, and the webhook attaches to it instead.
        if (await userManager.FindByEmailAsync(email) is { } existing)
        {
            if (await CanSignInAsync(existing))
            {
                return MembershipCheckoutResult.Fail(
                    "An account already exists for that email address. Please sign in first, then choose your plan.");
            }

            // A paid member who has not yet chosen a password. Their route in is the setup email,
            // not a second checkout.
            if (await GetCurrentMembershipAsync(existing.Id, ct) is not null)
                return MembershipCheckoutResult.Fail(AlreadyMemberMessage);
        }

        var now = DateTimeOffset.UtcNow;
        var join = await pendingJoins.GetLatestOpenByEmailAsync(normalized, ct);

        // A second submit for the same plan while the first checkout is still open — a
        // double-click, a back button, an impatient refresh — goes back to the same session.
        // No second row, no second provider session, nothing to reconcile.
        if (join is { Status: PendingJoinStatus.Pending, CheckoutUrl: not null } && join.HasLiveSession(now) && join.PlanId == plan.Id)
            return MembershipCheckoutResult.Ok(join.CheckoutUrl);

        // Checked here, once the shortcut above has ruled out "this person already holds a seat in
        // checkout" — their own row must not be the thing that makes the plan look full to them.
        if ((await GetPlanAvailabilityAsync(plan.Id, join?.Id, ct)).IsFull)
            return MembershipCheckoutResult.Full(PlanFullMessage);

        if (join is null)
        {
            join = new PendingJoin
            {
                Code = RandomNumberGenerator.GetString(JoinCodeAlphabet, 10),
                Email = email,
                EmailNormalized = normalized,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Country = request.Country,
            };
            await pendingJoins.AddAsync(join, ct);
        }

        // Otherwise the newest open row is reused in place: the form's latest answers win, and an
        // earlier session that is still payable is closed at the provider so only one exists.
        join.FirstName = request.FirstName;
        join.LastName = request.LastName;
        join.Country = request.Country;
        join.City = request.City;
        join.Phone = request.Phone;
        join.JobTitle = request.JobTitle;
        join.AddressLine1 = request.AddressLine1;
        join.AddressLine2 = request.AddressLine2;
        join.PostalCode = request.PostalCode;
        join.About = request.About;
        join.Expectations = request.Expectations;
        join.EarningsBand = request.EarningsBand;
        join.ReferralCode = request.ReferralCode;
        join.IpAddress = request.IpAddress;
        join.PurgedAt = null;

        // The code already redeemed for this row is kept if it is the same one typed again; a
        // different one is validated and redeemed afresh; none clears it.
        var typed = request.PromoCode?.Trim().ToUpperInvariant();
        var existingPromo = join.PromoCodeId is { } promoId ? await promoCodes.GetByIdAsync(promoId, ct) : null;
        if (string.IsNullOrEmpty(typed))
        {
            join.PromoCodeId = null;
        }
        else if (existingPromo is null || existingPromo.Code != typed || existingPromo.MembershipPlanId is { } onlyPlan && onlyPlan != plan.Id)
        {
            var (promo, promoError) = await ResolveMembershipPromoAsync(typed, plan, email, ct);
            if (promoError is not null)
                return MembershipCheckoutResult.Fail(promoError);
            join.PromoCodeId = promo?.Id;
        }

        return await OpenJoinSessionAsync(join, plan, successUrl, cancelUrlTemplate, ct);
    }

    public async Task<PendingJoinInfo?> GetPendingJoinByCodeAsync(string code, CancellationToken ct = default)
    {
        var join = await pendingJoins.GetByCodeAsync(code, ct);
        if (join is null) return null;

        var plan = await plans.GetByIdAsync(join.PlanId, ct);
        return new PendingJoinInfo(join.Code, join.FirstName, plan?.Name ?? "Membership",
            join.Status == PendingJoinStatus.Paid, join.ProviderSessionId);
    }

    public async Task<MembershipCheckoutResult> ResumeJoinCheckoutAsync(string code, string successUrl, string cancelUrlTemplate, CancellationToken ct = default)
    {
        var join = await pendingJoins.GetByCodeAsync(code, ct);
        if (join is null)
            return MembershipCheckoutResult.Fail("That link isn't recognised. Please start again from the membership page.");

        if (join.Status == PendingJoinStatus.Paid)
            return MembershipCheckoutResult.Fail("This checkout has already been completed.");

        // A superseded row was replaced by a later submit for the same address; the newest open
        // row is the one to continue, if there still is one. Otherwise this row is as good as any.
        if (join.Status == PendingJoinStatus.Superseded
            && await pendingJoins.GetLatestOpenByEmailAsync(join.EmailNormalized, ct) is { } newer)
        {
            join = newer;
        }

        var plan = await plans.GetByIdAsync(join.PlanId, ct);
        if (plan is null || plan.Status != MembershipPlanStatus.Active)
            return MembershipCheckoutResult.Fail("This membership plan isn't available any more. Please choose a plan from the membership page.");

        if (await userManager.FindByEmailAsync(join.Email) is { } existing)
        {
            if (await CanSignInAsync(existing))
                return MembershipCheckoutResult.Fail("An account already exists for that email address. Please sign in, then choose your plan.");

            if (await GetCurrentMembershipAsync(existing.Id, ct) is not null)
                return MembershipCheckoutResult.Fail(AlreadyMemberMessage);
        }

        // A lapsed session no longer holds a seat, so a resume competes for one like anyone else —
        // except against its own row, which may still be live.
        if (!(join.HasLiveSession(DateTimeOffset.UtcNow) && join.PlanId == plan.Id)
            && (await GetPlanAvailabilityAsync(plan.Id, join.Id, ct)).IsFull)
            return MembershipCheckoutResult.Full(PlanFullMessage);

        return await OpenJoinSessionAsync(join, plan, successUrl, cancelUrlTemplate, ct);
    }

    /// <summary>
    /// Opens the provider session for a pending join and records it on the row. The row is saved
    /// with no session *before* the provider is called, so a provider failure leaves an honest
    /// "no live session" state that the next submit simply retries — no orphans, no second row.
    /// </summary>
    private async Task<MembershipCheckoutResult> OpenJoinSessionAsync(
        PendingJoin join, MembershipPlan plan, string successUrl, string cancelUrlTemplate, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // The resume path can land here with the session still open (the email arrived late, or
        // the visitor came back through the cancel URL). Same plan, still payable: reuse it.
        if (join is { Status: PendingJoinStatus.Pending, CheckoutUrl: not null } && join.HasLiveSession(now) && join.PlanId == plan.Id)
            return MembershipCheckoutResult.Ok(join.CheckoutUrl);

        var previousSessionId = join.HasLiveSession(now) ? join.ProviderSessionId : null;

        join.PlanId = plan.Id;
        join.Status = PendingJoinStatus.Pending;
        join.ProviderSessionId = null;
        join.CheckoutUrl = null;
        join.SessionExpiresAt = null;
        join.UpdatedAt = now;
        await pendingJoins.SaveChangesAsync(ct);

        // One payable session per person. Left open, a forgotten tab on the old plan could still be
        // paid — and its webhook would then find a row that has moved on to a different session.
        if (previousSessionId is not null)
            await paymentProvider.ExpireCheckoutSessionAsync(previousSessionId, ct);
        // …and the money record that went with it is closed; a new one opens for this attempt.
        if (await transactions.GetLatestOpenForAsync(nameof(PendingJoin), join.Id, ct) is { } superseded)
            await transactions.CancelOpenAsync(superseded.Id, "Superseded by a newer checkout for the same join.", ct);
        var transaction = await transactions.OpenAsync(PaymentTransactionKind.Membership, null, nameof(PendingJoin), join.Id, plan.PriceMinor, plan.Currency, ct);

        string? couponId = null;
        if (join.PromoCodeId is { } joinPromoId && await promoCodes.GetByIdAsync(joinPromoId, ct) is { } joinPromo)
        {
            couponId = await EnsureCouponAsync(joinPromo, ct);
            if (couponId is null)
                return MembershipCheckoutResult.Fail("We couldn't apply your promo code just now — please try again in a moment.");
        }

        try
        {
            var session = await paymentProvider.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest(
                CustomerEmail: join.Email,
                ProductName: $"The VI House Membership — {plan.Name}",
                ProductDescription: PlanCheckoutDescription(plan),
                AmountMinor: plan.PriceMinor,
                Currency: plan.Currency,
                SuccessUrl: successUrl,
                CancelUrl: cancelUrlTemplate.Replace("{code}", join.Code),
                ClientReferenceId: join.Id.ToString(),
                Metadata: new Dictionary<string, string>
                {
                    ["pendingJoinId"] = join.Id.ToString(),
                    ["planId"] = plan.Id.ToString(),
                })
            {
                Recurring = ToRecurringInterval(plan.BillingPeriod),
                // A membership is an ongoing relationship and often a company expense: Stripe asks
                // for the phone and billing address, and offers company name and tax id optionally.
                ImageUrl = await MembershipImageUrlAsync(ct),
                CollectPhone = true,
                CollectBillingAddress = true,
                CollectCompanyDetails = true,
                ProviderPriceId = plan.IsProviderSynced ? plan.ProviderPriceId : null,
                ProviderCouponId = couponId,
            }, ct);

            join.ProviderSessionId = session.SessionId;
            join.CheckoutUrl = session.Url;
            // The provider's own figure. Its default for a subscription checkout is 24 hours; the
            // fallback only matters if it ever stops reporting one.
            join.SessionExpiresAt = session.ExpiresAt ?? now.AddHours(24);
            join.UpdatedAt = DateTimeOffset.UtcNow;
            await pendingJoins.SaveChangesAsync(ct);
            await transactions.AttachSessionAsync(transaction.Id, session.SessionId, null, ct);

            return MembershipCheckoutResult.Ok(session.Url);
        }
        catch (Exception ex)
        {
            await transactions.CancelOpenAsync(transaction.Id, "Checkout could not be opened at the provider.", ct);
            logger.LogWarning(ex, "Could not open a checkout session for pending join {PendingJoinId}", join.Id);
            return MembershipCheckoutResult.Fail("We couldn't reach the payment provider — please try again in a moment.");
        }
    }

    /// <summary>
    /// Validates and redeems a membership promo code. Mirrors PaymentService.TryApplyPromoAsync
    /// for tickets, plus the membership-only rules: scope, plan, the person it is reserved for,
    /// and (for a fixed amount) the plan's currency. Redemption is atomic and happens here, once,
    /// when a checkout is opened; the pending join remembers the code so a resume does not spend
    /// it twice. Returns (null, null) for no code.
    /// </summary>
    private async Task<(PromoCode? Promo, string? Error)> ResolveMembershipPromoAsync(string? code, MembershipPlan plan, string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code)) return (null, null);

        var promo = await promoCodes.GetByCodeAsync(code.Trim().ToUpperInvariant(), ct);
        if (promo is null || !promo.IsActive)
            return (null, "That promo code isn't valid.");
        if (promo.ExpiresAt is { } expires && expires < DateTimeOffset.UtcNow)
            return (null, "That promo code has expired.");
        if (promo.Scope != PromoScope.Memberships)
            return (null, "That promo code is for experience tickets, not membership.");
        if (promo.MembershipPlanId is { } onlyPlan && onlyPlan != plan.Id)
            return (null, "That promo code doesn't apply to this plan.");
        if (promo.RestrictedToEmail is { } reservedFor && reservedFor != NormalizeEmail(email))
            return (null, "That promo code is reserved for a different email address.");
        if (promo.Type == PromoCodeType.Fixed && !string.Equals(promo.Currency, plan.Currency, StringComparison.OrdinalIgnoreCase))
            return (null, "That promo code isn't valid for this plan's currency.");

        if (!await promoCodes.TryRedeemAsync(promo.Id, ct))
            return (null, "That promo code has reached its redemption limit.");

        return (promo, null);
    }

    /// <summary>What the first payment comes to after the promo, for the local payment row. The
    /// provider applied the coupon and is the record of what was actually charged; this keeps the
    /// admin's payment list from showing full price against a discounted sale.</summary>
    private static long DiscountedFirstPayment(MembershipPlan plan, PromoCode? promo) => promo switch
    {
        null => plan.PriceMinor,
        { Type: PromoCodeType.Percentage } => Math.Max(0, plan.PriceMinor - (long)Math.Round(plan.PriceMinor * Math.Min(100, promo.Value) / 100m, MidpointRounding.AwayFromZero)),
        _ => Math.Max(0, plan.PriceMinor - promo.Value),
    };

    /// <summary>The provider's coupon for a promo code, created on first use and remembered. Null
    /// when the provider could not be reached — the caller then refuses rather than silently
    /// selling at full price to someone who typed a valid code.</summary>
    private async Task<string?> EnsureCouponAsync(PromoCode promo, CancellationToken ct)
    {
        if (promo.ProviderCouponId is not null) return promo.ProviderCouponId;

        try
        {
            var id = await paymentProvider.CreateCouponAsync(new CouponRequest(
                Name: promo.Code,
                PercentOff: promo.Type == PromoCodeType.Percentage ? promo.Value : null,
                AmountOffMinor: promo.Type == PromoCodeType.Fixed ? promo.Value : null,
                Currency: promo.Currency,
                Forever: promo.MembershipDuration == PromoDuration.Forever), ct);

            promo.ProviderCouponId = id;
            promo.UpdatedAt = DateTimeOffset.UtcNow;
            await promoCodes.SaveChangesAsync(ct);
            return id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not create a provider coupon for promo code {Code}", promo.Code);
            return null;
        }
    }

    /// <summary>A user who can actually get in — as opposed to a ghost row the old join flow left
    /// behind, which has no password, no external login and no role that would let it through.</summary>
    private async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        if (await userManager.HasPasswordAsync(user)) return true;
        if ((await userManager.GetLoginsAsync(user)).Count > 0) return true;

        var roles = await userManager.GetRolesAsync(user);
        return roles.Any(r => r == Roles.Ambassador || Roles.AdminRoles.Contains(r));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    public async Task<int> PurgeStalePendingJoinsAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow - olderThan;
        var stale = await pendingJoins.ListPurgeableAsync(cutoff, take: 200, ct);
        if (stale.Count == 0) return 0;

        var now = DateTimeOffset.UtcNow;
        foreach (var join in stale)
        {
            // Name, email, plan and status stay: a very late payment on this row must still find
            // it, and the audit trail of "this address tried to join" is not personal data we
            // have no reason to keep. Everything the form asked beyond that goes.
            join.JobTitle = null;
            join.AddressLine1 = null;
            join.AddressLine2 = null;
            join.PostalCode = null;
            join.About = null;
            join.Expectations = null;
            join.EarningsBand = null;
            join.IpAddress = null;
            join.PurgedAt = now;
            // Deliberately not UpdatedAt: that would make the row look freshly touched.
        }

        await pendingJoins.SaveChangesAsync(ct);
        return stale.Count;
    }

    /// <summary>Shared tail of the signed-in checkout entry point: the local payment row first,
    /// then the provider session, then the row updated with the session id it must be matched on.</summary>
    private async Task<MembershipCheckoutResult> StartCheckoutAsync(
        MembershipPlan plan, ApplicationUser user, string? referralCode, PromoCode? promo, string successUrl, string cancelUrl, CancellationToken ct)
    {
        string? couponId = null;
        if (promo is not null)
        {
            couponId = await EnsureCouponAsync(promo, ct);
            if (couponId is null)
                return MembershipCheckoutResult.Fail("We couldn't apply your promo code just now — please try again in a moment.");
        }

        var payment = new MembershipPayment
        {
            UserId = user.Id,
            PlanId = plan.Id,
            AmountMinor = DiscountedFirstPayment(plan, promo),
            Currency = plan.Currency,
            Status = PaymentStatus.Created,
            ReferralCode = referralCode,
            PromoCodeId = promo?.Id,
        };
        payment.ProviderReference = $"pending_{payment.Id:N}"; // placeholder, unique — replaced once Stripe returns a session id
        await membershipPayments.AddAsync(payment, ct);
        await membershipPayments.SaveChangesAsync(ct);

        // The money record — opened Pending, moved only by the provider's events.
        var transaction = await transactions.OpenAsync(PaymentTransactionKind.Membership, user.Id, nameof(MembershipPayment), payment.Id, plan.PriceMinor, plan.Currency, ct);
        payment.TransactionId = transaction.Id;

        try
        {
            var session = await paymentProvider.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest(
                CustomerEmail: user.Email!,
                ProductName: $"The VI House Membership — {plan.Name}",
                ProductDescription: PlanCheckoutDescription(plan),
                AmountMinor: plan.PriceMinor,
                Currency: plan.Currency,
                SuccessUrl: successUrl,
                CancelUrl: cancelUrl,
                ClientReferenceId: payment.Id.ToString(),
                Metadata: new Dictionary<string, string>
                {
                    ["membershipPaymentId"] = payment.Id.ToString(),
                    ["planId"] = plan.Id.ToString(),
                    ["userId"] = user.Id.ToString(),
                })
            {
                // A Monthly/Annual plan becomes a real recurring subscription rather than a single
                // charge that silently lapses.
                Recurring = ToRecurringInterval(plan.BillingPeriod),
                // A membership is an ongoing relationship and often a company expense: Stripe asks
                // for the phone and billing address, and offers company name and tax id optionally.
                ImageUrl = await MembershipImageUrlAsync(ct),
                CollectPhone = true,
                CollectBillingAddress = true,
                CollectCompanyDetails = true,
                // Sell by the mirrored Stripe price when there is one, so the sale lands under the
                // named product in Stripe's reporting; the inline amount remains the fallback.
                ProviderPriceId = plan.IsProviderSynced ? plan.ProviderPriceId : null,
                ProviderCouponId = couponId,
                ProviderCustomerId = user.ProviderCustomerId,
            }, ct);

            payment.ProviderReference = session.SessionId;
            await membershipPayments.SaveChangesAsync(ct);
            await transactions.AttachSessionAsync(transaction.Id, session.SessionId, user.ProviderCustomerId, ct);

            return MembershipCheckoutResult.Ok(session.Url);
        }
        catch (Exception ex)
        {
            await transactions.CancelOpenAsync(transaction.Id, "Checkout could not be opened at the provider.", ct);
            logger.LogWarning(ex, "Could not open a checkout session for membership payment {PaymentId}", payment.Id);
            return MembershipCheckoutResult.Fail("We couldn't reach the payment provider — please try again in a moment.");
        }
    }

    public async Task<MembershipConfirmationInfo?> GetConfirmationBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        var payment = await membershipPayments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is not null)
        {
            var plan = await plans.GetByIdAsync(payment.PlanId, ct);

            // Local state only. The success page asks ICheckoutReconciliationService first, which
            // reads the provider and runs the same activation the webhook does; by the time this
            // is called, the row says whatever the provider said.
            if (payment.Status != PaymentStatus.Paid || payment.MembershipId is null)
                return new MembershipConfirmationInfo(false, plan?.Name, payment.AmountMinor, payment.Currency, null)
                {
                    AwaitingBank = payment.Status == PaymentStatus.Pending,
                };

            var membership = await memberships.GetByIdAsync(payment.MembershipId.Value, ct);
            return new MembershipConfirmationInfo(true, plan?.Name, payment.AmountMinor, payment.Currency, membership?.ExpiresAt)
            {
                UserId = payment.UserId,
            };
        }

        // A /join checkout has no payment row until the webhook writes one; the pending join is
        // what the success page can show in the meantime, priced from the plan.
        var join = await pendingJoins.GetBySessionAsync(sessionId, ct);
        if (join is null) return null;

        var joinPlan = await plans.GetByIdAsync(join.PlanId, ct);
        var amount = joinPlan?.PriceMinor ?? 0;
        var currency = joinPlan?.Currency ?? "GBP";

        if (join.Status != PendingJoinStatus.Paid || join.MembershipId is null)
            return new MembershipConfirmationInfo(false, joinPlan?.Name, amount, currency, null)
            {
                AwaitingBank = join.Status == PendingJoinStatus.Pending
                               && await transactions.GetBySessionAsync(sessionId, ct) is { Status: PaymentTransactionStatus.Processing },
            };

        var joined = await memberships.GetByIdAsync(join.MembershipId.Value, ct);
        return new MembershipConfirmationInfo(true, joinPlan?.Name, amount, currency, joined?.ExpiresAt)
        {
            UserId = join.UserId,
        };
    }

    // =============================================================================================
    // Webhooks
    // =============================================================================================

    public async Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        switch (webhookEvent.Type)
        {
            case PaymentWebhookEventType.CheckoutCompleted when webhookEvent.SessionId is not null:
                await HandleCheckoutCompletedAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.CheckoutCompletedAwaitingPayment when webhookEvent.SessionId is not null:
                await HandleCheckoutAwaitingPaymentAsync(webhookEvent, ct);
                break;
            // A delayed payment that never settled ends the same way an abandoned checkout does.
            case PaymentWebhookEventType.CheckoutExpired when webhookEvent.SessionId is not null:
                await HandleCheckoutExpiredAsync(webhookEvent.SessionId, paymentFailed: false, ct);
                break;
            case PaymentWebhookEventType.CheckoutPaymentFailed when webhookEvent.SessionId is not null:
                await HandleCheckoutExpiredAsync(webhookEvent.SessionId, paymentFailed: true, ct);
                break;
            case PaymentWebhookEventType.SubscriptionRenewed when webhookEvent.SubscriptionId is not null:
                await HandleSubscriptionRenewedAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.SubscriptionPaymentFailed when webhookEvent.SubscriptionId is not null:
                await HandleSubscriptionPaymentFailedAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.InvoicePaymentActionRequired when webhookEvent.SubscriptionId is not null:
                await HandleInvoiceActionRequiredAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.SubscriptionUpdated when webhookEvent.SubscriptionId is not null:
                await HandleSubscriptionUpdatedAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.SubscriptionCancelled when webhookEvent.SubscriptionId is not null:
                await HandleSubscriptionCancelledAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.ChargeRefunded when webhookEvent.PaymentIntentId is not null:
                await HandleRefundAsync(webhookEvent, ct);
                break;
        }
    }

    /// <summary>
    /// The buyer finished checkout with a delayed payment method. Nothing is activated: the
    /// signed-in payment row goes to Pending, a /join row stays Pending (its transaction says
    /// Processing), and the buyer is told the money is on its way. The outcome arrives later as
    /// CheckoutCompleted or CheckoutPaymentFailed.
    /// </summary>
    private async Task HandleCheckoutAwaitingPaymentAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var sessionId = webhookEvent.SessionId!;
        string? email, firstName; Guid? userId; Guid planId; string entityType; Guid entityId;

        var payment = await membershipPayments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is not null)
        {
            if (payment.Status != PaymentStatus.Created) return;
            payment.Status = PaymentStatus.Pending;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await membershipPayments.SaveChangesAsync(ct);

            var user = await userManager.FindByIdAsync(payment.UserId.ToString());
            if (user?.Email is null) return;
            (email, firstName, userId, planId, entityType, entityId) = (user.Email, user.FirstName, user.Id, payment.PlanId, nameof(MembershipPayment), payment.Id);
        }
        else
        {
            var join = await pendingJoins.GetBySessionAsync(sessionId, ct);
            if (join is null || join.Status != PendingJoinStatus.Pending) return;
            (email, firstName, userId, planId, entityType, entityId) = (join.Email, join.FirstName, null, join.PlanId, nameof(PendingJoin), join.Id);
        }

        var plan = await plans.GetByIdAsync(planId, ct);
        var what = plan?.Name ?? "your membership";
        await outbox.EnqueueEmailAsync(
            $"email:PaymentProcessing:{entityType}:{entityId}",
            "PaymentProcessing", email, "We've received your order — payment in progress",
            new PaymentProcessingEmailModel(firstName, what, webhookEvent.AmountMinor ?? plan?.PriceMinor ?? 0, webhookEvent.Currency ?? plan?.Currency ?? "GBP",
                SiteUrls.Absolute(BaseUrl, userId is null ? SiteUrls.Membership : SiteUrls.AccountMembership)),
            entityType, entityId, ct);

        if (userId is { } id)
        {
            await outbox.EnqueueNotificationAsync(
                $"notification:PaymentProcessing:{entityType}:{entityId}",
                id, NotificationType.Payment,
                "Payment In Progress", $"Your bank is confirming your payment for {what}. Your membership starts as soon as it lands.",
                SiteUrls.AccountMembership, entityType, entityId, ct);
        }
    }

    /// <summary>
    /// A renewal needs the member to authenticate (3-D Secure) before the bank will pay it. The
    /// membership is untouched — the paid-up period still runs — and the member gets the
    /// provider's hosted page, which is where the confirmation happens. One email per invoice.
    /// </summary>
    private async Task HandleInvoiceActionRequiredAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var membership = await FindBySubscriptionAsync(webhookEvent.SubscriptionId!, ct);
        if (membership is null || membership.Status == MembershipStatus.Cancelled) return;

        var user = await userManager.FindByIdAsync(membership.UserId.ToString());
        if (user?.Email is null) return;

        var plan = await plans.GetByIdAsync(membership.PlanId, ct);
        var accountUrl = SiteUrls.Absolute(BaseUrl, SiteUrls.AccountMembership);
        var actionUrl = webhookEvent.HostedInvoiceUrl ?? accountUrl;
        var key = webhookEvent.InvoiceId ?? webhookEvent.EventId;

        await outbox.EnqueueEmailAsync(
            $"email:MembershipPaymentActionRequired:Invoice:{key}",
            "MembershipPaymentActionRequired", user.Email, "Please confirm your membership payment",
            new MembershipPaymentFailedEmailModel(user.FirstName, plan?.Name ?? "Membership", actionUrl, membership.ExpiresAt, null),
            nameof(Membership), membership.Id, ct);

        await outbox.EnqueueNotificationAsync(
            $"notification:MembershipPaymentActionRequired:Invoice:{key}",
            user.Id, NotificationType.Payment,
            "Confirm Your Payment", $"Your bank needs you to confirm the renewal of your {plan?.Name ?? "membership"}.",
            SiteUrls.AccountMembership, nameof(Membership), membership.Id, ct);
    }

    /// <summary>
    /// The subscription changed at the provider — through the billing portal, the dashboard, or
    /// on its own as it moves between statuses. What is mirrored: the period end (authoritative
    /// for the expiry), whether it will stop at the period end (no renewal date to show), and the
    /// provider's status where it maps onto ours. "canceled" is left to the deleted event, which
    /// always follows it.
    /// </summary>
    private async Task HandleSubscriptionUpdatedAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var membership = await FindBySubscriptionAsync(webhookEvent.SubscriptionId!, ct);
        if (membership is null || membership.Status == MembershipStatus.Cancelled) return;
        if (webhookEvent.SubscriptionStatus is "canceled" or "incomplete" or "incomplete_expired") return;

        var now = DateTimeOffset.UtcNow;
        var before = new { membership.Status, membership.ExpiresAt, membership.RenewalAt };

        if (webhookEvent.CurrentPeriodEnd is { } periodEnd && periodEnd > now)
            membership.ExpiresAt = periodEnd;

        membership.RenewalAt = webhookEvent.CancelAtPeriodEnd == true ? null : membership.ExpiresAt;
        membership.ProviderCustomerId ??= webhookEvent.CustomerId;

        switch (webhookEvent.SubscriptionStatus)
        {
            case "past_due":
            case "paused":
                if (membership.Status == MembershipStatus.Active) membership.Status = MembershipStatus.PastDue;
                break;
            case "active":
            case "trialing":
                if (membership.Status == MembershipStatus.PastDue) membership.Status = MembershipStatus.Active;
                break;
            case "unpaid":
                // Every retry has been made and the provider has stopped trying: the provider's own
                // guidance is to revoke. The row ends now; the deleted event, if the dashboard is set
                // to cancel as well, finds nothing left to do.
                membership.Status = MembershipStatus.Cancelled;
                membership.CancelledAt = now;
                membership.RenewalAt = null;
                membership.ExpiresAt = now;
                break;
        }

        if (before.Status == membership.Status && before.ExpiresAt == membership.ExpiresAt && before.RenewalAt == membership.RenewalAt)
            return;

        membership.UpdatedAt = now;
        await memberships.SaveChangesAsync(ct);
        logger.LogInformation("Membership {MembershipId} mirrored subscription update {EventId}: {From} → {To}, expires {ExpiresAt}, renews {RenewalAt}.",
            membership.Id, webhookEvent.EventId, before.Status, membership.Status, membership.ExpiresAt, membership.RenewalAt);

        if (membership.Status == MembershipStatus.Cancelled && await userManager.FindByIdAsync(membership.UserId.ToString()) is { } user)
        {
            await SetMemberStatusAsync(user, MemberStatus.Cancelled);
            var plan = await plans.GetByIdAsync(membership.PlanId, ct);
            await AnnounceMembershipEndedAsync(user, membership.Id, plan?.Name ?? "membership", now, wasRevoked: false, ct);
        }
    }

    /// <summary>
    /// Money went back to the buyer on a membership charge. The payment row says so; the
    /// membership itself is left as it is — whether a refund ends access at once, at the period
    /// end, or not at all is a policy the House has not set, so a human is told and decides. The
    /// member hears about the refund itself, once per amount.
    /// </summary>
    private async Task HandleRefundAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var transaction = await transactions.GetByPaymentIntentAsync(webhookEvent.PaymentIntentId!, ct);
        if (transaction is not { Kind: PaymentTransactionKind.Membership or PaymentTransactionKind.MembershipRenewal }) return;

        var payment = (await membershipPayments.FindAsync(p => p.TransactionId == transaction.Id, ct)).FirstOrDefault();
        if (payment is null || payment.Status == PaymentStatus.Refunded) return;

        var refunded = webhookEvent.AmountRefundedMinor ?? 0;
        var full = refunded >= (webhookEvent.AmountMinor ?? payment.AmountMinor);
        payment.Status = full ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        await membershipPayments.SaveChangesAsync(ct);

        var plan = await plans.GetByIdAsync(payment.PlanId, ct);
        var user = await userManager.FindByIdAsync(payment.UserId.ToString());
        var what = plan?.Name ?? "your membership";

        if (user?.Email is not null)
        {
            await outbox.EnqueueEmailAsync(
                $"email:PaymentRefunded:MembershipPayment:{payment.Id}:{refunded}",
                "PaymentRefunded", user.Email, full ? "Your refund is on its way" : "A partial refund is on its way",
                new PaymentRefundedEmailModel(user.FirstName, what, refunded, payment.Currency, !full,
                    "Your membership is unchanged for now; if anything about it needs to change, we'll be in touch separately."),
                nameof(MembershipPayment), payment.Id, ct);

            await outbox.EnqueueNotificationAsync(
                $"notification:PaymentRefunded:MembershipPayment:{payment.Id}:{refunded}",
                user.Id, NotificationType.Payment,
                full ? "Payment Refunded" : "Partial Refund", $"{(full ? "A full" : "A partial")} refund for {what} is on its way back to you.",
                SiteUrls.AccountMembership, nameof(MembershipPayment), payment.Id, ct);
        }

        var contact = await ContactEmailAsync(ct);
        if (!string.IsNullOrWhiteSpace(contact))
        {
            await outbox.EnqueueEmailAsync(
                $"email:RefundReview:MembershipPayment:{payment.Id}:{refunded}",
                "ContactMessage", contact, $"Membership payment refunded — decide about access ({user?.Email ?? "unknown"})",
                new ContactMessageEmailModel("The VI House (system)", user?.Email ?? "unknown", "Membership refund",
                    $"{(full ? "A full" : "A partial")} refund of {refunded / 100m:0.00} {payment.Currency} was issued on the {what} payment by {user?.FirstName} {user?.LastName} ({user?.Email}).\n" +
                    $"Membership {payment.MembershipId}: access was NOT changed automatically. If the membership should end, revoke it from the admin members page or cancel the subscription in the provider dashboard."),
                nameof(MembershipPayment), payment.Id, ct);
        }
    }

    private async Task SetMemberStatusAsync(ApplicationUser user, MemberStatus status)
    {
        if (user.MemberStatus == status) return;
        user.MemberStatus = status;
        await userManager.UpdateAsync(user);
    }

    private async Task HandleCheckoutCompletedAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var sessionId = webhookEvent.SessionId!;
        Guid.TryParse(webhookEvent.ClientReferenceId, out var referenceId);

        // Signed-in purchase: the payment row was written before the session was opened. Matched
        // by session id, or — if the provider call timed out after succeeding, leaving the
        // placeholder reference behind — by the row id we handed the provider.
        var payment = await membershipPayments.GetByProviderReferenceAsync(sessionId, ct)
                      ?? (referenceId == Guid.Empty ? null : await membershipPayments.GetByIdAsync(referenceId, ct));
        if (payment is not null)
        {
            await HandleMemberCheckoutCompletedAsync(payment, webhookEvent, ct);
            return;
        }

        // Otherwise a /join checkout, matched the same two ways.
        var join = await pendingJoins.GetBySessionAsync(sessionId, ct)
                   ?? (referenceId == Guid.Empty ? null : await pendingJoins.GetByIdAsync(referenceId, ct));
        if (join is null)
            return; // unknown session — a ticket or seminar checkout, which PaymentService/SeminarService own

        // The claim is the idempotency: the delivery that flips the row to Paid is the one that
        // provisions, and any other delivery of the same event finds nothing to claim. All three
        // open states are claimable — a payment that lands on a superseded or lapsed row is still
        // money, and money that arrived must never be ignored.
        if (!await pendingJoins.TryClaimAsync(join.Id, [PendingJoinStatus.Pending, PendingJoinStatus.Superseded, PendingJoinStatus.Expired], PendingJoinStatus.Paid, ct))
            return;

        join.Status = PendingJoinStatus.Paid;

        try
        {
            await ActivateFromPendingJoinAsync(join, webhookEvent, ct);
        }
        catch
        {
            // Half-provisioned is the one state that must not persist: the claim is handed back so
            // the provider's retry (the exception becomes a 500) runs this again from the top.
            // Every step in ActivateFromPendingJoinAsync is written to be re-entrant for that reason.
            await pendingJoins.TryClaimAsync(join.Id, [PendingJoinStatus.Paid], PendingJoinStatus.Pending, ct);
            throw;
        }
    }

    /// <summary>The signed-in path: the payment row already exists, the account already exists.</summary>
    private async Task HandleMemberCheckoutCompletedAsync(MembershipPayment payment, PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        if (!await membershipPayments.TryClaimAsync(payment.Id, PaymentStatus.Created, PaymentStatus.Paid, ct))
            return; // already handled, or never a live checkout

        payment.Status = PaymentStatus.Paid;
        payment.ProviderReference = webhookEvent.SessionId!;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        var plan = await plans.GetByIdAsync(payment.PlanId, ct);
        if (plan is null) return;

        var user = await userManager.FindByIdAsync(payment.UserId.ToString());
        await ActivateMembershipAsync(payment, plan, user, webhookEvent, ct);
    }

    /// <summary>
    /// The account comes into being here, because here is where there is a payment to attach it
    /// to. Every step finds-or-creates, so a crash partway (and the provider's retry) picks up
    /// where it left off rather than duplicating anything.
    /// </summary>
    private async Task ActivateFromPendingJoinAsync(PendingJoin join, PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var sessionId = webhookEvent.SessionId!;
        var plan = await plans.GetByIdAsync(join.PlanId, ct)
                   ?? throw new InvalidOperationException($"Pending join {join.Id} references a plan that no longer exists.");
        var now = DateTimeOffset.UtcNow;

        var user = await userManager.FindByEmailAsync(join.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = join.Email,
                Email = join.Email,
                // Unconfirmed: nobody has vetted this address. Choosing a password through the
                // emailed setup link is what proves it (see ResetPassword).
                EmailConfirmed = false,
                FirstName = join.FirstName,
                LastName = join.LastName,
                Country = join.Country,
                City = join.City,
                // Collected on the join form and normalised there; unconfirmed in the same sense the
                // address is, since nothing has been sent to it yet.
                // The form's number, or the one Stripe validated on its own page if the form was
                // skipped (a resumed checkout, a link straight to the provider).
                PhoneNumber = join.Phone ?? webhookEvent.CustomerPhone,
                // Not PendingApplication: this account exists because money landed.
                MemberStatus = MemberStatus.Active,
            };

            // No password is ever set or communicated — the member chooses one via the setup link.
            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded)
                throw new InvalidOperationException($"Could not create the account for pending join {join.Id}: {string.Join(" ", created.Errors.Select(e => e.Description))}");
        }
        else
        {
            // A ghost from an earlier attempt, or a signed-in retry of this same webhook. The form
            // is the freshest word on who they are.
            user.FirstName = join.FirstName;
            user.LastName = join.LastName;
            user.Country = join.Country;
            user.City ??= join.City;
            // The form is the freshest word on the number too — a member who re-joins from a new
            // phone should be reachable on that one, not on the one from two years ago.
            if (Prefer(join.Phone, webhookEvent.CustomerPhone) is { } number) user.PhoneNumber = number;
            user.MemberStatus = MemberStatus.Active;
            await userManager.UpdateAsync(user);
        }

        // Two tabs, both paid: the person already holds a current membership by the time the
        // second completion arrives. The money is real and goes in the ledger against the
        // membership they have; the duplicate subscription is stopped at the provider so it never
        // bills again; the first charge is left for a human to refund, with a trail to find it by.
        if (await GetCurrentMembershipAsync(user.Id, ct) is { } existingMembership)
        {
            await RecordDuplicatePurchaseAsync(join, user, plan, existingMembership, webhookEvent, ct);
            return;
        }

        // Everything the form asked for is kept, not just on the first join: someone who let a
        // membership lapse and joined again has just re-typed their details, and the newer answer is
        // the true one. Blank answers never overwrite something already on file — an optional field
        // left empty this time is not a request to forget what was there.
        if (await profiles.GetByUserIdAsync(user.Id, ct) is { } profile)
        {
            profile.JobTitle = Prefer(join.JobTitle, profile.JobTitle);
            profile.AddressLine1 = Prefer(join.AddressLine1, profile.AddressLine1);
            profile.AddressLine2 = Prefer(join.AddressLine2, profile.AddressLine2);
            profile.PostalCode = Prefer(join.PostalCode, profile.PostalCode);
            profile.About = Prefer(join.About, profile.About);
            profile.Expectations = Prefer(join.Expectations, profile.Expectations);
            profile.EarningsBand = Prefer(join.EarningsBand, profile.EarningsBand);
            profile.Company = Prefer(webhookEvent.CompanyName, profile.Company);
            profile.TaxId = Prefer(webhookEvent.TaxId, profile.TaxId);
            profile.UpdatedAt = now;
        }
        else
        {
            await profiles.AddAsync(new Profile
            {
                UserId = user.Id,
                JobTitle = join.JobTitle,
                AddressLine1 = join.AddressLine1,
                AddressLine2 = join.AddressLine2,
                PostalCode = join.PostalCode,
                About = join.About,
                Expectations = join.Expectations,
                EarningsBand = join.EarningsBand,
                // Whatever the buyer chose to give Stripe — usually nothing, for a personal purchase.
                Company = webhookEvent.CompanyName,
                TaxId = webhookEvent.TaxId,
                UpdatedAt = now,
            }, ct);
        }
        await profiles.SaveChangesAsync(ct);

        static string? Prefer(string? fromForm, string? onFile) =>
            string.IsNullOrWhiteSpace(fromForm) ? onFile : fromForm;

        // The terms tick on the form, recorded now that there is a user to record it against.
        await consentRecords.AddAsync(new ConsentRecord
        {
            UserId = user.Id,
            Type = ConsentType.TermsOfService,
            Granted = true,
            Text = TermsConsentText,
            GrantedAt = join.CreatedAt,
            IpAddress = join.IpAddress,
        }, ct);
        await consentRecords.SaveChangesAsync(ct);

        var payment = await membershipPayments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is null)
        {
            var joinPromo = join.PromoCodeId is { } paidPromoId ? await promoCodes.GetByIdAsync(paidPromoId, ct) : null;
            payment = new MembershipPayment
            {
                UserId = user.Id,
                PlanId = plan.Id,
                AmountMinor = DiscountedFirstPayment(plan, joinPromo),
                Currency = plan.Currency,
                Status = PaymentStatus.Paid,
                ProviderReference = sessionId,
                ReferralCode = join.ReferralCode,
                PromoCodeId = join.PromoCodeId,
            };
            await membershipPayments.AddAsync(payment, ct);
            await membershipPayments.SaveChangesAsync(ct);
        }

        // The money record was opened against the PendingJoin before an account existed; now it
        // has an owner, and the payment row points at it.
        if (await transactions.GetBySessionAsync(sessionId, ct) is { } joinTransaction)
        {
            await transactions.AssignUserAsync(joinTransaction.Id, user.Id, webhookEvent.CustomerId, ct);
            if (payment.TransactionId is null)
            {
                payment.TransactionId = joinTransaction.Id;
                await membershipPayments.SaveChangesAsync(ct);
            }
        }

        var membership = await ActivateMembershipAsync(payment, plan, user, webhookEvent, ct);

        join.UserId = user.Id;
        join.MembershipId = membership.Id;
        join.PaidAt = now;
        join.ProviderSessionId ??= sessionId;
        join.UpdatedAt = now;
        await pendingJoins.SaveChangesAsync(ct);

        await SupersedeOtherJoinsAsync(join, ct);
    }

    private async Task RecordDuplicatePurchaseAsync(
        PendingJoin join, ApplicationUser user, MembershipPlan plan, Membership existing, PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var sessionId = webhookEvent.SessionId!;
        var now = DateTimeOffset.UtcNow;

        if (await membershipPayments.GetByProviderReferenceAsync(sessionId, ct) is null)
        {
            await membershipPayments.AddAsync(new MembershipPayment
            {
                UserId = user.Id,
                PlanId = plan.Id,
                MembershipId = existing.Id,
                AmountMinor = plan.PriceMinor,
                Currency = plan.Currency,
                Status = PaymentStatus.Paid,
                ProviderReference = sessionId,
                ReferralCode = join.ReferralCode,
                TransactionId = (await transactions.GetBySessionAsync(sessionId, ct))?.Id,
            }, ct);
            await membershipPayments.SaveChangesAsync(ct);
        }

        var cancelled = false;
        if (webhookEvent.SubscriptionId is { } duplicateSubscription && duplicateSubscription != existing.ProviderSubscriptionId)
            cancelled = await paymentProvider.CancelSubscriptionAsync(duplicateSubscription, ct);

        logger.LogWarning(
            "Duplicate membership purchase: user {UserId} paid session {SessionId} while membership {MembershipId} is current. Duplicate subscription {SubscriptionId} cancelled: {Cancelled}",
            user.Id, sessionId, existing.Id, webhookEvent.SubscriptionId, cancelled);

        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = Guid.Empty, // system, not a person
            Action = "DuplicateMembershipPurchase",
            EntityType = nameof(Membership),
            EntityId = existing.Id,
            DataAfter = JsonSerializer.Serialize(new
            {
                UserId = user.Id,
                user.Email,
                SessionId = sessionId,
                webhookEvent.SubscriptionId,
                DuplicateSubscriptionCancelled = cancelled,
                plan.Name,
                plan.PriceMinor,
                plan.Currency,
            }),
        }, ct);
        await auditLogs.SaveChangesAsync(ct);

        var contact = await ContactEmailAsync(ct);
        if (!string.IsNullOrWhiteSpace(contact))
        {
            await outbox.EnqueueEmailAsync(
                $"email:DuplicateMembership:PendingJoin:{join.Id}",
                "ContactMessage", contact, "Duplicate membership payment needs a refund",
                new ContactMessageEmailModel("The VI House (system)", user.Email!, "Duplicate membership purchase",
                    $"{user.FirstName} {user.LastName} ({user.Email}) paid for {plan.Name} a second time while membership {existing.Id} was already current.\n" +
                    $"Provider session: {sessionId}. Duplicate subscription: {webhookEvent.SubscriptionId ?? "none"} (cancelled at provider: {(cancelled ? "yes" : "no — check manually")}).\n" +
                    "Nothing was refunded automatically. Please refund the second charge from the provider dashboard."),
                nameof(Membership), existing.Id, ct);
        }

        join.UserId = user.Id;
        join.MembershipId = existing.Id;
        join.PaidAt = now;
        join.ProviderSessionId ??= sessionId;
        join.UpdatedAt = now;
        await pendingJoins.SaveChangesAsync(ct);

        await SupersedeOtherJoinsAsync(join, ct);
    }

    /// <summary>Once one row for an address has paid, every other open row for it is closed —
    /// including at the provider, so no forgotten tab can take a second payment.</summary>
    private async Task SupersedeOtherJoinsAsync(PendingJoin paid, CancellationToken ct)
    {
        var others = await pendingJoins.ListByEmailAsync(paid.EmailNormalized, [PendingJoinStatus.Pending, PendingJoinStatus.Expired], paid.Id, ct);
        if (others.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var other in others)
        {
            if (other.HasLiveSession(now))
                await paymentProvider.ExpireCheckoutSessionAsync(other.ProviderSessionId!, ct);

            other.Status = PendingJoinStatus.Superseded;
            other.UpdatedAt = now;
        }

        await pendingJoins.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Shared tail of both completion paths: the membership row, the payment linked to it, the
    /// Member role, the emails. The account may be null only on the signed-in path, if the user
    /// vanished between checkout and webhook — the membership is still recorded against the id.
    /// </summary>
    private async Task<Membership> ActivateMembershipAsync(
        MembershipPayment payment, MembershipPlan plan, ApplicationUser? user, PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Re-entrant: a retry after a crash further down finds the membership it already wrote.
        var membership = payment.MembershipId is { } linked ? await memberships.GetByIdAsync(linked, ct) : null;
        membership ??= webhookEvent.SubscriptionId is { } subscriptionId ? await FindBySubscriptionAsync(subscriptionId, ct) : null;

        if (membership is null)
        {
            DateTimeOffset? expiresAt = plan.BillingPeriod switch
            {
                MembershipBillingPeriod.Monthly => now.AddMonths(1),
                MembershipBillingPeriod.Annual => now.AddYears(1),
                _ => null, // OneTime — no expiry
            };

            membership = new Membership
            {
                UserId = payment.UserId,
                PlanId = payment.PlanId,
                StartAt = now,
                RenewalAt = expiresAt,
                ExpiresAt = expiresAt,
                Status = MembershipStatus.Active,
                // What the renewal and cancellation webhooks, and the billing portal, will look up.
                ProviderSubscriptionId = webhookEvent.SubscriptionId,
                ProviderCustomerId = webhookEvent.CustomerId,
            };
            await memberships.AddAsync(membership, ct);
            await memberships.SaveChangesAsync(ct);

            // Money that arrived is never refused, but two checkouts racing for the last seat can
            // both land. Leave a trail rather than a silent overshoot.
            if (plan.MaxMembers is { } max)
            {
                var availability = await GetPlanAvailabilityAsync(plan.Id, ct);
                if (availability.Taken > max)
                {
                    logger.LogWarning("Plan {PlanId} is over its member limit ({Taken}/{Max}) after membership {MembershipId}.", plan.Id, availability.Taken, max, membership.Id);
                    await auditLogs.AddAsync(new AuditLogEntry
                    {
                        AdminUserId = Guid.Empty,
                        Action = "MembershipCapExceeded",
                        EntityType = nameof(MembershipPlan),
                        EntityId = plan.Id,
                        DataAfter = JsonSerializer.Serialize(new { MembershipId = membership.Id, availability.Taken, Max = max }),
                    }, ct);
                    await auditLogs.SaveChangesAsync(ct);
                }
            }
        }

        if (payment.MembershipId != membership.Id)
        {
            payment.MembershipId = membership.Id;
            payment.UpdatedAt = now;
        }
        await membershipPayments.SaveChangesAsync(ct);

        await ambassadorService.RecordConversionAsync(payment.ReferralCode, ReferralConversionKind.MembershipPurchase,
            nameof(MembershipPayment), payment.Id, payment.AmountMinor, payment.Currency, ct: ct);

        if (user is null) return membership;

        if (!await userManager.IsInRoleAsync(user, Roles.Member))
            await userManager.AddToRoleAsync(user, Roles.Member);

        if (user.MemberStatus != MemberStatus.Active)
        {
            user.MemberStatus = MemberStatus.Active;
            await userManager.UpdateAsync(user);
        }

        // Someone who joined through /join has no password at all. The setup link is emailed — and
        // only emailed: the success page is reachable by anyone holding the session id from the
        // URL, which is not a thing to hand a password link to. Sent from here because this is the
        // path that always runs, webhook or server-side read.
        if (!await userManager.HasPasswordAsync(user))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            // Same unpadded URL-safe alphabet as WebEncoders.Base64UrlEncode, which is what the
            // ResetPassword page decodes with — using the framework primitive here keeps the
            // Business layer free of an ASP.NET Core dependency.
            var encoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));
            var setupUrl = SiteUrls.Absolute(BaseUrl, SiteUrls.ResetPassword(encoded));

            await outbox.EnqueueEmailAsync(
                $"email:WelcomeSetup:User:{user.Id}",
                "WelcomeSetup", user.Email!, "Set up your VI House account",
                new WelcomeSetupEmailModel(user.FirstName, setupUrl, plan.Name),
                nameof(ApplicationUser), user.Id, ct);
        }

        await outbox.EnqueueEmailAsync(
            $"email:MembershipConfirmed:Membership:{membership.Id}",
            "MembershipConfirmed", user.Email!, $"Welcome — you're a {plan.Name}",
            new MembershipConfirmedEmailModel(user.FirstName, plan.Name, membership.ExpiresAt)
            {
                AmountMinor = payment.AmountMinor,
                Currency = payment.Currency,
                MemberNumber = $"VIH-{membership.Id:N}"[..12].ToUpperInvariant(),
                AccountUrl = SiteUrls.Absolute(BaseUrl, SiteUrls.AccountMembership),
            },
            nameof(Membership), membership.Id, ct);

        // The number the provider validated on its checkout page wins over whatever is on the
        // account; SmsService logs (and does nothing) when there is no gateway or no number.
        await outbox.EnqueueSmsAsync(
            $"sms:MembershipConfirmed:Membership:{membership.Id}",
            "MembershipConfirmed", webhookEvent.CustomerPhone ?? user.PhoneNumber,
            $"The VI House: payment received. You're confirmed as a {plan.Name}"
                + (membership.ExpiresAt is { } until ? $" until {until:d MMM yyyy}" : "")
                + ". Your card, sessions and experiences are open in your account.",
            nameof(Membership), membership.Id, ct);

        await outbox.EnqueueNotificationAsync(
            $"notification:MembershipConfirmed:Membership:{membership.Id}",
            user.Id, NotificationType.Payment,
            "Membership Confirmed", $"You're confirmed as a {plan.Name}.",
            SiteUrls.Account, nameof(Membership), membership.Id, ct);

        return membership;
    }

    /// <summary>An "expired" only ends a checkout nobody finished; a completed one waiting on the
    /// bank ends only through a payment failure. See PaymentService for the same rule.</summary>
    private async Task HandleCheckoutExpiredAsync(string sessionId, bool paymentFailed, CancellationToken ct)
    {
        var payment = await membershipPayments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is not null)
        {
            if (payment.Status != PaymentStatus.Created && !(paymentFailed && payment.Status == PaymentStatus.Pending)) return;

            payment.Status = PaymentStatus.Cancelled;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await membershipPayments.SaveChangesAsync(ct);
            return;
        }

        var join = await pendingJoins.GetBySessionAsync(sessionId, ct);
        if (join is null) return;

        // A join whose checkout completed on a delayed method is still Pending here but Processing
        // on the money side; a stray expiry must not close it. A payment failure does.
        if (!paymentFailed && await transactions.GetBySessionAsync(sessionId, ct) is { Status: PaymentTransactionStatus.Processing })
            return;

        // Only a Pending row lapses. Paid stays paid; Superseded stays superseded (its replacement
        // is the live one, and this event is usually our own doing — see OpenJoinSessionAsync).
        if (!await pendingJoins.TryClaimAsync(join.Id, [PendingJoinStatus.Pending], PendingJoinStatus.Expired, ct))
            return;

        join.Status = PendingJoinStatus.Expired;

        // One nudge per abandoned form, and none at all if the person has since got in some other
        // way — a later row that paid, or an account that already holds a membership.
        if (join.ResumeEmailSentAt is not null) return;
        if (await pendingJoins.AnyPaidForEmailAsync(join.EmailNormalized, ct)) return;
        if (await userManager.FindByEmailAsync(join.Email) is { } user && await GetCurrentMembershipAsync(user.Id, ct) is not null) return;

        var plan = await plans.GetByIdAsync(join.PlanId, ct);
        var resumeUrl = SiteUrls.Absolute(BaseUrl, SiteUrls.JoinResume(join.Code));

        await outbox.EnqueueEmailAsync(
            $"email:MembershipResume:PendingJoin:{join.Id}",
            "MembershipResume", join.Email, "Pick up where you left off",
            new MembershipResumeEmailModel(join.FirstName, plan?.Name ?? "Membership", resumeUrl),
            nameof(PendingJoin), join.Id, ct);

        join.ResumeEmailSentAt = DateTimeOffset.UtcNow;
        join.UpdatedAt = join.ResumeEmailSentAt;
        await pendingJoins.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A later billing cycle paid: the same row runs on, its expiry pushed to the end of the period
    /// just paid for. Idempotent by construction — the date only ever moves forward, so a
    /// redelivered event changes nothing.
    /// </summary>
    private async Task HandleSubscriptionRenewedAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var membership = await FindBySubscriptionAsync(webhookEvent.SubscriptionId!, ct);
        if (membership is null) return;

        // The payment row written below doubles as the idempotency key: Stripe retries webhooks,
        // and a second delivery of the same invoice must not extend the term twice. Keyed on the
        // invoice, not the event — one invoice can raise more than one paid event, and each of
        // those has its own event id.
        var receipt = $"renewal_{webhookEvent.InvoiceId ?? webhookEvent.EventId}";
        if (await membershipPayments.GetByProviderReferenceAsync(receipt, ct) is not null) return;

        var plan = await plans.GetByIdAsync(membership.PlanId, ct);
        var now = DateTimeOffset.UtcNow;

        // The provider's period end when it sent one; otherwise one more period from whichever is
        // later of "now" and the current expiry, so a late-arriving webhook never shortens a term.
        var baseline = membership.ExpiresAt is { } current && current > now ? current : now;
        var newExpiry = webhookEvent.CurrentPeriodEnd ?? (plan?.BillingPeriod switch
        {
            MembershipBillingPeriod.Monthly => baseline.AddMonths(1),
            MembershipBillingPeriod.Annual => baseline.AddYears(1),
            _ => baseline,
        });

        var wasPastDue = membership.Status == MembershipStatus.PastDue;

        membership.ExpiresAt = newExpiry;
        membership.RenewalAt = newExpiry;
        membership.Status = MembershipStatus.Active;
        membership.CancelledAt = null;
        membership.ProviderCustomerId ??= webhookEvent.CustomerId;
        membership.UpdatedAt = now;
        await memberships.SaveChangesAsync(ct);

        // A renewal also records a payment row, so the member's history and the admin's payment
        // list both show every charge — not just the first.
        await membershipPayments.AddAsync(new MembershipPayment
        {
            UserId = membership.UserId,
            PlanId = membership.PlanId,
            MembershipId = membership.Id,
            // What the invoice actually collected — a coupon, a proration or a price change makes
            // that differ from the plan's list price, and the ledger records money, not list prices.
            // The plan is only the fallback for an event that carried no amount.
            AmountMinor = webhookEvent.AmountMinor ?? plan?.PriceMinor ?? 0,
            Currency = webhookEvent.Currency ?? plan?.Currency ?? "GBP",
            Status = PaymentStatus.Paid,
            ProviderReference = receipt,
            // The MembershipRenewal transaction the dispatcher opened for this invoice a moment ago.
            TransactionId = webhookEvent.InvoiceId is { } invoiceId ? (await transactions.GetByInvoiceAsync(invoiceId, ct))?.Id : null,
        }, ct);
        await membershipPayments.SaveChangesAsync(ct);

        if (await userManager.FindByIdAsync(membership.UserId.ToString()) is { } user)
        {
            await outbox.EnqueueEmailAsync(
                $"email:MembershipRenewed:{receipt}",
                "MembershipRenewed", user.Email!, $"Your {plan?.Name ?? "membership"} has renewed",
                new MembershipRenewedEmailModel(user.FirstName, plan?.Name ?? "Membership", newExpiry),
                nameof(Membership), membership.Id, ct);

            await outbox.EnqueueNotificationAsync(
                $"notification:MembershipRenewed:{receipt}",
                user.Id, NotificationType.Payment,
                wasPastDue ? "Payment Received" : "Membership Renewed",
                $"Your {plan?.Name ?? "membership"} now runs until {newExpiry:d MMMM yyyy}.",
                SiteUrls.Account, nameof(Membership), membership.Id, ct);
        }
    }

    /// <summary>
    /// A renewal charge was declined. The membership is marked PastDue but keeps its ExpiresAt —
    /// the member has paid up to that date and keeps access until it. The provider retries the
    /// card on its own schedule and raises this event every time; the nudge is keyed on the
    /// invoice, so the member is told once per invoice however many attempts it takes. A
    /// successful retry arrives as a renewal and sets things right; giving up arrives as an
    /// "unpaid" update or a cancellation.
    /// </summary>
    private async Task HandleSubscriptionPaymentFailedAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var membership = await FindBySubscriptionAsync(webhookEvent.SubscriptionId!, ct);
        if (membership is null || membership.Status is not (MembershipStatus.Active or MembershipStatus.PastDue)) return;

        var now = DateTimeOffset.UtcNow;
        if (membership.Status == MembershipStatus.Active)
        {
            membership.Status = MembershipStatus.PastDue;
            membership.UpdatedAt = now;
            await memberships.SaveChangesAsync(ct);
        }
        var invoiceKey = webhookEvent.InvoiceId ?? webhookEvent.EventId;

        var user = await userManager.FindByIdAsync(membership.UserId.ToString());
        if (user is null) return;

        var plan = await plans.GetByIdAsync(membership.PlanId, ct);
        var accountUrl = SiteUrls.Absolute(BaseUrl, SiteUrls.AccountMembership);

        // The hosted invoice is a direct "pay this" page and needs nothing configured; the billing
        // portal is the fallback (it returns null until it has been set up in the dashboard); the
        // account page is the fallback's fallback.
        var actionUrl = webhookEvent.HostedInvoiceUrl;
        if (actionUrl is null && membership.ProviderCustomerId is not null)
            actionUrl = await paymentProvider.CreateBillingPortalUrlAsync(membership.ProviderCustomerId, accountUrl, ct);
        actionUrl ??= accountUrl;

        await outbox.EnqueueEmailAsync(
            $"email:MembershipPaymentFailed:Invoice:{invoiceKey}",
            "MembershipPaymentFailed", user.Email!, "Your membership payment didn't go through",
            new MembershipPaymentFailedEmailModel(user.FirstName, plan?.Name ?? "Membership", actionUrl, membership.ExpiresAt, webhookEvent.NextPaymentAttempt),
            nameof(Membership), membership.Id, ct);

        await outbox.EnqueueNotificationAsync(
            $"notification:MembershipPaymentFailed:Invoice:{invoiceKey}",
            user.Id, NotificationType.Payment,
            "Payment Needs Attention",
            membership.ExpiresAt is { } until
                ? $"Your {plan?.Name ?? "membership"} renewal was declined. Update your card to keep access beyond {until:d MMMM yyyy}."
                : $"Your {plan?.Name ?? "membership"} renewal was declined. Please update your card.",
            SiteUrls.AccountMembership, nameof(Membership), membership.Id, ct);
    }

    /// <summary>
    /// The subscription has ended at the provider. A member who cancels through the portal is
    /// stopped at the period end, and the provider sends this event <em>then</em>, not on the
    /// click (the click arrives as an update with cancel_at_period_end, mirrored above); so by the
    /// time this arrives the paid-for period is over and the row closes now. An immediate
    /// cancellation from the dashboard, or the provider giving up on an unpaid card, ends access
    /// now as well — there is no subscription left to have paid for anything further.
    /// </summary>
    private async Task HandleSubscriptionCancelledAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var membership = await FindBySubscriptionAsync(webhookEvent.SubscriptionId!, ct);
        if (membership is null || membership.Status == MembershipStatus.Cancelled) return;

        var now = DateTimeOffset.UtcNow;
        membership.Status = MembershipStatus.Cancelled;
        membership.CancelledAt = now;
        membership.RenewalAt = null;
        // Close it now rather than leaving a future expiry on a cancelled row; the entitlement
        // check reads Status first anyway, so this is bookkeeping rather than a second lock.
        if (membership.ExpiresAt is null || membership.ExpiresAt > now)
            membership.ExpiresAt = now;
        membership.UpdatedAt = now;
        await memberships.SaveChangesAsync(ct);

        if (await userManager.FindByIdAsync(membership.UserId.ToString()) is { } user)
        {
            // Only when nothing else keeps them a member — a complimentary grant, say.
            if (await GetCurrentMembershipAsync(user.Id, ct) is null)
                await SetMemberStatusAsync(user, MemberStatus.Cancelled);
            var plan = await plans.GetByIdAsync(membership.PlanId, ct);
            await AnnounceMembershipEndedAsync(user, membership.Id, plan?.Name ?? "membership", now, wasRevoked: false, ct);
        }
    }

    /// <summary>The in-app line and the email, together — a membership ending is the one event a
    /// member most needs to hear about in plain words, whichever way it happened.</summary>
    private async Task AnnounceMembershipEndedAsync(ApplicationUser user, Guid membershipId, string planName, DateTimeOffset endedAt, bool wasRevoked, CancellationToken ct)
    {
        await outbox.EnqueueNotificationAsync(
            $"notification:MembershipEnded:Membership:{membershipId}",
            user.Id, NotificationType.Payment,
            "Membership Ended", $"Your {planName} has ended. You're welcome back any time.",
            SiteUrls.Membership, nameof(Membership), membershipId, ct);

        if (user.Email is not null)
        {
            await outbox.EnqueueEmailAsync(
                $"email:MembershipEnded:Membership:{membershipId}",
                "MembershipEnded", user.Email, "Your membership has ended",
                new MembershipEndedEmailModel(user.FirstName, planName, endedAt, SiteUrls.Absolute(BaseUrl, SiteUrls.Membership), wasRevoked),
                nameof(ApplicationUser), user.Id, ct);
        }
    }

    private async Task<Membership?> FindBySubscriptionAsync(string subscriptionId, CancellationToken ct) =>
        (await memberships.FindAsync(m => m.ProviderSubscriptionId == subscriptionId, ct))
            .OrderByDescending(m => m.StartAt)
            .FirstOrDefault();

    /// <summary>
    /// The House's own mark, as the picture every membership is sold under. An uploaded logo wins
    /// (Admin &gt; Site &amp; SEO); the committed app icon is the fallback, so a fresh install still
    /// shows a brand rather than a grey box. Absolute, because the provider fetches it itself.
    /// </summary>
    private async Task<string> MembershipImageUrlAsync(CancellationToken ct)
    {
        var settings = await siteSettings.GetCachedAsync(ct);
        var path = settings.LogoStorageKey is not null
            ? $"/media/site-logo/{settings.Id}"
            : settings.LogoUrl;

        if (string.IsNullOrWhiteSpace(path)) return SiteUrls.Absolute(BaseUrl, "/icons/icon-512.png");
        return path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path : SiteUrls.Absolute(BaseUrl, path);
    }

    /// <summary>
    /// What sits under the name on the checkout page: how long the membership runs and how often it
    /// is charged. An admin's own description keeps its place in front of it.
    /// </summary>
    private static string BillingLine(MembershipPlan plan) => plan.BillingPeriod switch
    {
        MembershipBillingPeriod.Monthly => "Membership · renews monthly until cancelled",
        MembershipBillingPeriod.Annual => "Membership · renews yearly until cancelled",
        _ => "Membership · one payment, no renewal",
    };

    private static string PlanCheckoutDescription(MembershipPlan plan) =>
        string.IsNullOrWhiteSpace(plan.Description) ? BillingLine(plan) : $"{plan.Description.Trim()} — {BillingLine(plan)}";

    private string BaseUrl => siteOptions.Value.BaseUrl.TrimEnd('/');

    /// <summary>The team inbox — the admin-edited site setting first, the config value behind it.</summary>
    private async Task<string?> ContactEmailAsync(CancellationToken ct)
    {
        var settings = await siteSettings.GetCachedAsync(ct);
        var fromSettings = settings.ContactEmail;
        return string.IsNullOrWhiteSpace(fromSettings) ? siteOptions.Value.ContactEmail : fromSettings;
    }

    // =============================================================================================
    // Helpers
    // =============================================================================================

    private static RecurringInterval? ToRecurringInterval(MembershipBillingPeriod period) => period switch
    {
        MembershipBillingPeriod.Monthly => RecurringInterval.Monthly,
        MembershipBillingPeriod.Annual => RecurringInterval.Annual,
        _ => null, // OneTime — a single charge, no renewal
    };

    private static object Snapshot(MembershipPlan plan) => new
    {
        plan.Name, plan.PriceMinor, plan.Currency, plan.BillingPeriod, plan.Status, plan.MaxMembers, plan.ProviderProductId, plan.ProviderPriceId,
        plan.IncludesCommunity, plan.IncludesSessions, plan.IncludesDirectory, plan.IncludesMemberCard, plan.DiscordRoleId,
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(MembershipPlan),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
