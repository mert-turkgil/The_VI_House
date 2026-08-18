using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Membership;

namespace VIHouse.Business.Concrete;

public class MembershipService(
    IRepository<MembershipPlan> plans,
    IRepository<Membership> memberships,
    IMembershipPaymentRepository membershipPayments,
    IPaymentProvider paymentProvider,
    IEmailService emailService,
    IAuditLogRepository auditLogs,
    UserManager<ApplicationUser> userManager) : IMembershipService
{
    public async Task<List<MembershipPlan>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var active = await plans.FindAsync(p => p.Status == MembershipPlanStatus.Active, ct);
        return active.OrderBy(p => p.SortOrder).ToList();
    }

    public async Task<List<MembershipPlan>> GetAllPlansAsync(CancellationToken ct = default) =>
        (await plans.GetAllAsync(ct)).OrderBy(p => p.SortOrder).ToList();

    public Task<MembershipPlan?> GetPlanAsync(Guid id, CancellationToken ct = default) => plans.GetByIdAsync(id, ct);

    public async Task<MembershipPlan> CreatePlanAsync(MembershipPlan plan, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        await plans.AddAsync(plan, ct);
        await LogAsync("MembershipPlanCreated", plan.Id, adminUserId, ipAddress,
            before: null, after: new { plan.Name, plan.PriceMinor, plan.Currency, plan.BillingPeriod, plan.Status }, ct);
        await plans.SaveChangesAsync(ct);
        return plan;
    }

    public async Task UpdatePlanAsync(MembershipPlan updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await plans.GetByIdAsync(updated.Id, ct)
            ?? throw new InvalidOperationException($"Membership plan {updated.Id} not found.");

        var before = new { existing.Name, existing.PriceMinor, existing.Currency, existing.BillingPeriod, existing.Status };

        existing.Name = updated.Name;
        existing.Description = updated.Description;
        existing.PriceMinor = updated.PriceMinor;
        existing.Currency = updated.Currency;
        existing.BillingPeriod = updated.BillingPeriod;
        existing.Features = updated.Features;
        existing.Status = updated.Status;
        existing.SortOrder = updated.SortOrder;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("MembershipPlanUpdated", existing.Id, adminUserId, ipAddress,
            before, new { existing.Name, existing.PriceMinor, existing.Currency, existing.BillingPeriod, existing.Status }, ct);

        // No explicit Update() call: `existing` is already tracked, loaded on this same scoped
        // DbContext — same reasoning as ExperienceService.UpdateCoreFieldsAsync.
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
    }

    public async Task<Membership?> GetCurrentMembershipAsync(Guid userId, CancellationToken ct = default)
    {
        var mine = await memberships.FindAsync(m => m.UserId == userId, ct);
        return mine
            .Where(m => m.Status == MembershipStatus.Active)
            .OrderByDescending(m => m.StartAt)
            .FirstOrDefault();
    }

    public async Task<MembershipCheckoutResult> InitiateCheckoutAsync(Guid planId, Guid userId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var plan = await plans.GetByIdAsync(planId, ct);
        if (plan is null || plan.Status != MembershipPlanStatus.Active)
            return MembershipCheckoutResult.Fail("This membership plan isn't available right now.");

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return MembershipCheckoutResult.Fail("Your account couldn't be found — please log in again.");

        var payment = new MembershipPayment
        {
            UserId = userId,
            PlanId = planId,
            AmountMinor = plan.PriceMinor,
            Currency = plan.Currency,
            Status = PaymentStatus.Created,
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
                    ["planId"] = planId.ToString(),
                    ["userId"] = userId.ToString(),
                }), ct);

            payment.ProviderReference = session.SessionId;
            await membershipPayments.SaveChangesAsync(ct);

            return MembershipCheckoutResult.Ok(session.Url);
        }
        catch (Exception)
        {
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
        return new MembershipConfirmationInfo(true, plan?.Name, payment.AmountMinor, payment.Currency, membership?.ExpiresAt);
    }

    public async Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        if (webhookEvent.Type == PaymentWebhookEventType.CheckoutCompleted && webhookEvent.SessionId is not null)
            await HandleCheckoutCompletedAsync(webhookEvent.SessionId, ct);
        else if (webhookEvent.Type == PaymentWebhookEventType.CheckoutExpired && webhookEvent.SessionId is not null)
            await HandleCheckoutExpiredAsync(webhookEvent.SessionId, ct);
    }

    private async Task HandleCheckoutCompletedAsync(string sessionId, CancellationToken ct)
    {
        var payment = await membershipPayments.GetByProviderReferenceAsync(sessionId, ct);
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
        };
        await memberships.AddAsync(membership, ct);
        await memberships.SaveChangesAsync(ct);

        payment.MembershipId = membership.Id;
        await membershipPayments.SaveChangesAsync(ct);

        if (await userManager.FindByIdAsync(payment.UserId.ToString()) is { } user)
        {
            if (!await userManager.IsInRoleAsync(user, Roles.Member))
                await userManager.AddToRoleAsync(user, Roles.Member);

            await emailService.SendAsync(
                "MembershipConfirmed", user.Email!, $"Welcome — you're a {plan.Name}",
                new MembershipConfirmedEmailModel(user.FirstName, plan.Name, membership.ExpiresAt),
                nameof(Membership), membership.Id, ct);
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
