using VIHouse.Entities.Membership;

namespace VIHouse.Business.Abstract;

public interface IMembershipService
{
    Task<List<MembershipPlan>> GetActivePlansAsync(CancellationToken ct = default);
    Task<List<MembershipPlan>> GetAllPlansAsync(CancellationToken ct = default);
    Task<MembershipPlan?> GetPlanAsync(Guid id, CancellationToken ct = default);
    Task<MembershipPlan> CreatePlanAsync(MembershipPlan plan, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task UpdatePlanAsync(MembershipPlan updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task ArchivePlanAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Most recent Active membership for a user, if any — null means never purchased or lapsed.</summary>
    Task<Membership?> GetCurrentMembershipAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Requires an existing account (brief: purchase happens post-login, no guest checkout — a membership isn't preceded by an Application that already captured name/email the way event tickets are).</summary>
    Task<MembershipCheckoutResult> InitiateCheckoutAsync(Guid planId, Guid userId, string successUrl, string cancelUrl, CancellationToken ct = default);

    /// <summary>Reads LOCAL state only, same "never trust the browser redirect alone" rule as the ticket-purchase flow.</summary>
    Task<MembershipConfirmationInfo?> GetConfirmationBySessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Deliberately does NOT touch the shared ProcessedWebhookEvent ledger that PaymentService uses —
    /// idempotency here comes purely from MembershipPayment.Status, so this is safe to call
    /// unconditionally alongside PaymentService's own webhook handling with zero risk of the two
    /// interfering with each other's idempotency bookkeeping.
    /// </summary>
    Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default);
}

public record MembershipCheckoutResult(bool Success, string? CheckoutUrl, string? Error)
{
    public static MembershipCheckoutResult Ok(string url) => new(true, url, null);
    public static MembershipCheckoutResult Fail(string error) => new(false, null, error);
}

public record MembershipConfirmationInfo(bool IsConfirmed, string? PlanName, long AmountMinor, string Currency, DateTimeOffset? ExpiresAt);
