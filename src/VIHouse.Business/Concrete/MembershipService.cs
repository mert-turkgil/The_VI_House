using System.Buffers.Text;
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
using VIHouse.Entities.Membership;
using VIHouse.Entities.Notifications;
using VIHouse.Entities.Users;

namespace VIHouse.Business.Concrete;

public class MembershipService(
    IRepository<MembershipPlan> plans,
    IRepository<Membership> memberships,
    IMembershipPaymentRepository membershipPayments,
    IProfileRepository profiles,
    IPaymentProvider paymentProvider,
    IPaymentCatalogProvider catalog,
    IEmailService emailService,
    INotificationService notificationService,
    IAuditLogRepository auditLogs,
    IOptions<SiteOptions> siteOptions,
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
                plan.Name, plan.Description, plan.PriceMinor, plan.Currency,
                ToRecurringInterval(plan.BillingPeriod), Active: true), ct);

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
    public async Task<Membership?> GetCurrentMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var mine = await memberships.FindAsync(m => m.UserId == userId, ct);

        return mine
            .Where(m => m.Status == MembershipStatus.Active)
            // A null ExpiresAt is a one-time membership that does not lapse, not an expired one.
            .Where(m => m.ExpiresAt is null || m.ExpiresAt > now)
            .OrderByDescending(m => m.StartAt)
            .FirstOrDefault();
    }

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

    public async Task<MembershipCheckoutResult> InitiateCheckoutAsync(Guid planId, Guid userId, string? referralCode, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var plan = await plans.GetByIdAsync(planId, ct);
        if (plan is null || plan.Status != MembershipPlanStatus.Active)
            return MembershipCheckoutResult.Fail("This membership plan isn't available right now.");

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return MembershipCheckoutResult.Fail("Your account couldn't be found — please log in again.");

        // One current membership per person. Buying a second while the first still runs would
        // open two rows and, for a recurring plan, two subscriptions billing side by side.
        if (await GetCurrentMembershipAsync(userId, ct) is not null)
            return MembershipCheckoutResult.Fail("You already hold an active membership. To change plan, manage your billing from your account page or contact us.");

        return await StartCheckoutAsync(plan, user, referralCode, successUrl, cancelUrl, ct);
    }

    public async Task<MembershipCheckoutResult> InitiateJoinCheckoutAsync(JoinRequest request, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var plan = await plans.GetByIdAsync(request.PlanId, ct);
        if (plan is null || plan.Status != MembershipPlanStatus.Active)
            return MembershipCheckoutResult.Fail("This membership plan isn't available right now.");

        // Refusing rather than reusing: attaching this payment to an existing account would let
        // anyone who knows a member's email buy "for" them, and would hand the payer an onboarding
        // link to an account that isn't theirs.
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return MembershipCheckoutResult.Fail(
                "An account already exists for that email address. Please sign in first, then choose your plan.");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            // Unconfirmed on purpose: unlike the application route, nobody has vetted this person,
            // so the address is unproven until they click the link in the onboarding email.
            EmailConfirmed = false,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Country = request.Country,
            City = request.City,
            MemberStatus = MemberStatus.PendingApplication,
        };

        // No password is ever set or communicated. The member creates their own during onboarding,
        // via a reset token — so an abandoned checkout leaves behind an account nobody can sign in
        // to, rather than one with a guessable or emailed credential.
        var created = await userManager.CreateAsync(user);
        if (!created.Succeeded)
            return MembershipCheckoutResult.Fail(string.Join(" ", created.Errors.Select(e => e.Description)));

        // The form's answers become the profile straight away, so the member never has to type
        // them a second time on the account page — and so the House has them even if the checkout
        // is abandoned and someone follows up by hand.
        await profiles.AddAsync(new Profile
        {
            UserId = user.Id,
            JobTitle = request.JobTitle,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            PostalCode = request.PostalCode,
            About = request.About,
            Expectations = request.Expectations,
            EarningsBand = request.EarningsBand,
            UpdatedAt = DateTimeOffset.UtcNow,
        }, ct);
        await profiles.SaveChangesAsync(ct);

        return await StartCheckoutAsync(plan, user, request.ReferralCode, successUrl, cancelUrl, ct);
    }

    /// <summary>Shared tail of both checkout entry points: the local payment row first, then the
    /// provider session, then the row updated with the session id it must be matched on.</summary>
    private async Task<MembershipCheckoutResult> StartCheckoutAsync(
        MembershipPlan plan, ApplicationUser user, string? referralCode, string successUrl, string cancelUrl, CancellationToken ct)
    {
        var payment = new MembershipPayment
        {
            UserId = user.Id,
            PlanId = plan.Id,
            AmountMinor = plan.PriceMinor,
            Currency = plan.Currency,
            Status = PaymentStatus.Created,
            ReferralCode = referralCode,
        };
        payment.ProviderReference = $"pending_{payment.Id:N}"; // placeholder, unique — replaced once Stripe returns a session id
        await membershipPayments.AddAsync(payment, ct);
        await membershipPayments.SaveChangesAsync(ct);

        try
        {
            var session = await paymentProvider.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest(
                CustomerEmail: user.Email!,
                ProductName: $"The VI House Membership — {plan.Name}",
                ProductDescription: plan.Description,
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
                // Sell by the mirrored Stripe price when there is one, so the sale lands under the
                // named product in Stripe's reporting; the inline amount remains the fallback.
                ProviderPriceId = plan.IsProviderSynced ? plan.ProviderPriceId : null,
            }, ct);

            payment.ProviderReference = session.SessionId;
            await membershipPayments.SaveChangesAsync(ct);

            return MembershipCheckoutResult.Ok(session.Url);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not open a checkout session for membership payment {PaymentId}", payment.Id);
            return MembershipCheckoutResult.Fail("We couldn't reach the payment provider — please try again in a moment.");
        }
    }

    public async Task<MembershipConfirmationInfo?> GetConfirmationBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        var payment = await membershipPayments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is null) return null;

        var plan = await plans.GetByIdAsync(payment.PlanId, ct);

        if (payment.Status != PaymentStatus.Paid || payment.MembershipId is null)
            return new MembershipConfirmationInfo(false, plan?.Name, payment.AmountMinor, payment.Currency, null);

        var membership = await memberships.GetByIdAsync(payment.MembershipId.Value, ct);
        return new MembershipConfirmationInfo(true, plan?.Name, payment.AmountMinor, payment.Currency, membership?.ExpiresAt)
        {
            UserId = payment.UserId,
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
            case PaymentWebhookEventType.CheckoutExpired when webhookEvent.SessionId is not null:
                await HandleCheckoutExpiredAsync(webhookEvent.SessionId, ct);
                break;
            case PaymentWebhookEventType.SubscriptionRenewed when webhookEvent.SubscriptionId is not null:
                await HandleSubscriptionRenewedAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.SubscriptionCancelled when webhookEvent.SubscriptionId is not null:
                await HandleSubscriptionCancelledAsync(webhookEvent, ct);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var payment = await membershipPayments.GetByProviderReferenceAsync(webhookEvent.SessionId!, ct);
        if (payment is null || payment.Status == PaymentStatus.Paid)
            return; // unknown session (e.g. a ticket-purchase session — see PaymentService), or already handled

        var plan = await plans.GetByIdAsync(payment.PlanId, ct);
        if (plan is null) return;

        payment.Status = PaymentStatus.Paid;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        var now = DateTimeOffset.UtcNow;
        DateTimeOffset? expiresAt = plan.BillingPeriod switch
        {
            MembershipBillingPeriod.Monthly => now.AddMonths(1),
            MembershipBillingPeriod.Annual => now.AddYears(1),
            _ => null, // OneTime — no expiry
        };

        var membership = new Membership
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

        payment.MembershipId = membership.Id;
        await membershipPayments.SaveChangesAsync(ct);

        if (await userManager.FindByIdAsync(payment.UserId.ToString()) is { } user)
        {
            if (!await userManager.IsInRoleAsync(user, Roles.Member))
                await userManager.AddToRoleAsync(user, Roles.Member);

            if (user.MemberStatus != MemberStatus.Active)
            {
                user.MemberStatus = MemberStatus.Active;
                await userManager.UpdateAsync(user);
            }

            // Someone who joined through /join has no password at all — the browser redirect shows
            // them a setup link, but that tab is easily lost, so the same link is emailed. Sent from
            // the webhook rather than the redirect because this is the path that always runs.
            if (!await userManager.HasPasswordAsync(user))
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                // Same unpadded URL-safe alphabet as WebEncoders.Base64UrlEncode, which is what the
                // ResetPassword page decodes with — using the framework primitive here keeps the
                // Business layer free of an ASP.NET Core dependency.
                var encoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));
                var setupUrl = $"{siteOptions.Value.BaseUrl.TrimEnd('/')}/Identity/Account/ResetPassword?code={encoded}";

                await emailService.SendAsync(
                    "WelcomeSetup", user.Email!, "Set up your VI House account",
                    new WelcomeSetupEmailModel(user.FirstName, setupUrl, plan.Name),
                    nameof(ApplicationUser), user.Id, ct);
            }

            await emailService.SendAsync(
                "MembershipConfirmed", user.Email!, $"Welcome — you're a {plan.Name}",
                new MembershipConfirmedEmailModel(user.FirstName, plan.Name, membership.ExpiresAt),
                nameof(Membership), membership.Id, ct);

            await notificationService.CreateForUserAsync(
                user.Id, NotificationType.Payment,
                "Membership Confirmed", $"You're confirmed as a {plan.Name}.",
                "/account", ct);
        }
    }

    private async Task HandleCheckoutExpiredAsync(string sessionId, CancellationToken ct)
    {
        var payment = await membershipPayments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is null || payment.Status == PaymentStatus.Paid)
            return;

        payment.Status = PaymentStatus.Cancelled;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        await membershipPayments.SaveChangesAsync(ct);
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
        // and a second delivery of the same invoice must not extend the term twice.
        var receipt = $"renewal_{webhookEvent.EventId}";
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
            AmountMinor = plan?.PriceMinor ?? 0,
            Currency = plan?.Currency ?? "GBP",
            Status = PaymentStatus.Paid,
            ProviderReference = receipt,
        }, ct);
        await membershipPayments.SaveChangesAsync(ct);

        if (await userManager.FindByIdAsync(membership.UserId.ToString()) is { } user)
        {
            await emailService.SendAsync(
                "MembershipRenewed", user.Email!, $"Your {plan?.Name ?? "membership"} has renewed",
                new MembershipRenewedEmailModel(user.FirstName, plan?.Name ?? "Membership", newExpiry),
                nameof(Membership), membership.Id, ct);

            await notificationService.CreateForUserAsync(
                user.Id, NotificationType.Payment,
                "Membership Renewed", $"Your {plan?.Name ?? "membership"} now runs until {newExpiry:d MMMM yyyy}.",
                "/account", ct);
        }
    }

    /// <summary>
    /// The subscription has ended at the provider. The membership is marked Cancelled but keeps
    /// its ExpiresAt: a member who cancels mid-period has paid for the period and keeps access
    /// until it ends — Stripe sends this event at the end of the period, not on the click.
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
            var plan = await plans.GetByIdAsync(membership.PlanId, ct);
            await notificationService.CreateForUserAsync(
                user.Id, NotificationType.Payment,
                "Membership Ended", $"Your {plan?.Name ?? "membership"} has ended. You're welcome back any time.",
                "/membership", ct);
        }
    }

    private async Task<Membership?> FindBySubscriptionAsync(string subscriptionId, CancellationToken ct) =>
        (await memberships.FindAsync(m => m.ProviderSubscriptionId == subscriptionId, ct))
            .OrderByDescending(m => m.StartAt)
            .FirstOrDefault();

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
        plan.Name, plan.PriceMinor, plan.Currency, plan.BillingPeriod, plan.Status, plan.ProviderProductId, plan.ProviderPriceId,
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
