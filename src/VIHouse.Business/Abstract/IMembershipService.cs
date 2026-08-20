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

    /// <summary>For a visitor who is already signed in. referralCode comes from the /r/{code} cookie, if present — see Application.ReferralCode for the equivalent on the ticket-purchase side.</summary>
    Task<MembershipCheckoutResult> InitiateCheckoutAsync(Guid planId, Guid userId, string? referralCode, string successUrl, string cancelUrl, CancellationToken ct = default);

    /// <summary>
    /// Join-and-pay in one step for someone with no account yet: the form's details provision a
    /// pending account, then checkout begins against it. The account exists before payment (it has
    /// to — the payment must be attributable to someone) but is unusable until the webhook confirms
    /// the money: no password is ever set, the email is unconfirmed, and the onboarding gate blocks
    /// every signed-in page until 2FA is set up.
    ///
    /// Returns a failure if the email already belongs to an account, rather than silently attaching
    /// a stranger's payment to it.
    /// </summary>
    Task<MembershipCheckoutResult> InitiateJoinCheckoutAsync(JoinRequest request, string successUrl, string cancelUrl, CancellationToken ct = default);

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

public record MembershipConfirmationInfo(bool IsConfirmed, string? PlanName, long AmountMinor, string Currency, DateTimeOffset? ExpiresAt)
{
    /// <summary>Set once the payment is confirmed, so the success page can hand the new member
    /// straight into onboarding without asking them to log in first (they have no password yet).</summary>
    public Guid? UserId { get; init; }
}

public record JoinRequest(
    Guid PlanId,
    string FirstName,
    string LastName,
    string Email,
    string Country,
    string? City,
    string? ReferralCode);
