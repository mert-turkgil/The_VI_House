using VIHouse.Entities.Membership;

namespace VIHouse.Business.Abstract;

public interface IMembershipService
{
    // --- Plans (admin) ----------------------------------------------------------------------------
    //
    // Every write here is pushed to the payment provider's catalogue as well (IPaymentCatalogProvider).
    // The push is best-effort: a failure is recorded on the plan (ProviderSyncError) and never
    // blocks the local save, because a plan an admin cannot edit while Stripe is down is worse than
    // a plan whose mirror is a minute behind. SyncPlanAsync / SyncAllPlansAsync are the retry.

    Task<List<MembershipPlan>> GetActivePlansAsync(CancellationToken ct = default);
    Task<List<MembershipPlan>> GetAllPlansAsync(CancellationToken ct = default);
    Task<MembershipPlan?> GetPlanAsync(Guid id, CancellationToken ct = default);
    Task<MembershipPlan> CreatePlanAsync(MembershipPlan plan, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task UpdatePlanAsync(MembershipPlan updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task ArchivePlanAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Removes the plan row outright. Refused while any membership or payment references it —
    /// those are financial history, and the FK is Restrict for exactly that reason; archive
    /// instead. The provider mirror is archived, not deleted, because a product that has carried a
    /// price cannot be deleted there.
    /// </summary>
    Task<PlanMutationResult> DeletePlanAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Pushes one plan to the provider now. The retry for a failed automatic sync, and
    /// the way to mirror a plan that predates the catalogue.</summary>
    Task<PlanMutationResult> SyncPlanAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Pushes every plan, archived ones included (so a retired plan is retired at the
    /// provider too). Reports counts rather than stopping at the first failure.</summary>
    Task<PlanSyncSummary> SyncAllPlansAsync(Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Creates a local plan for every product at the provider that has no row here yet. Never
    /// updates an existing row: the local plan is the source of truth, and an import that
    /// overwrote prices would make the provider's dashboard a second, competing place to edit.
    /// Imported plans arrive Archived so nothing goes on sale by accident.
    /// </summary>
    Task<PlanImportSummary> ImportPlansFromProviderAsync(Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>How many memberships and payments reference the plan — what decides whether it may
    /// be deleted, and what the admin screen shows next to the delete button.</summary>
    Task<PlanUsage> GetPlanUsageAsync(Guid id, CancellationToken ct = default);

    // --- Membership (admin) ------------------------------------------------------------------------

    /// <summary>
    /// Gives a user a membership without a purchase. Refused while they already hold a current
    /// one (revoke first — two overlapping memberships would make "which plan am I on" ambiguous)
    /// and, unless <paramref name="overrideCap"/>, while the plan is at its member limit. A null
    /// <paramref name="expiresAt"/> means the plan's own period from today; for a one-time plan
    /// that is "never".
    /// </summary>
    Task<PlanMutationResult> GrantComplimentaryAsync(Guid userId, Guid planId, DateTimeOffset? expiresAt, bool overrideCap, string? note, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Ends the user's current membership today. Refused when the provider holds a live
    /// subscription for it: cancelling locally while Stripe keeps billing would charge someone
    /// for nothing, so that one is ended from the billing portal and arrives here as a webhook.
    /// </summary>
    Task<PlanMutationResult> RevokeMembershipAsync(Guid userId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- Membership (member) -----------------------------------------------------------------------

    /// <summary>
    /// How many of a plan's seats are taken, against its MaxMembers. "Taken" includes seats in
    /// checkout — a pending join with a live provider session, a signed-in checkout opened in the
    /// last day — the same way a ticket hold counts against inventory, so two people cannot both
    /// be sold the last place. Unlimited plans report IsFull = false and Remaining = null.
    /// </summary>
    Task<PlanAvailability> GetPlanAvailabilityAsync(Guid planId, CancellationToken ct = default);

    /// <summary>Most recent Active membership for a user, if any — null means never purchased or lapsed.</summary>
    Task<Membership?> GetCurrentMembershipAsync(Guid userId, CancellationToken ct = default);

    /// <summary>What the user's current plan opens — the tier flags on MembershipPlan, resolved
    /// for one person. Null when they hold no current membership. This is what the account area,
    /// the directory and the sessions gate read; "has a membership" alone no longer decides.</summary>
    Task<MemberEntitlements?> GetEntitlementsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>The current membership together with its plan, for the account and membership
    /// pages. Null when there is no current membership.</summary>
    Task<MembershipSummary?> GetMembershipSummaryAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Every membership the user has ever held, newest first — the account page's history.</summary>
    Task<List<MembershipSummary>> GetMembershipHistoryAsync(Guid userId, CancellationToken ct = default);

    /// <summary>A one-time link into the provider's billing portal for the user's current
    /// subscription, or null when there is nothing to manage there (no recurring membership, or
    /// the provider cannot open one right now).</summary>
    Task<string?> CreateBillingPortalUrlAsync(Guid userId, string returnUrl, CancellationToken ct = default);

    /// <summary>For a visitor who is already signed in. referralCode comes from the /r/{code} cookie, if present — see Application.ReferralCode for the equivalent on the ticket-purchase side.</summary>
    Task<MembershipCheckoutResult> InitiateCheckoutAsync(Guid planId, Guid userId, string? referralCode, string? promoCode, string successUrl, string cancelUrl, CancellationToken ct = default);

    /// <summary>
    /// Join-and-pay in one step for someone with no account yet. The form is held as a PendingJoin
    /// and checkout begins against that row; nothing with a uniqueness constraint — user, profile,
    /// membership — is written until the provider's webhook confirms the money. So a retry cannot
    /// collide with an earlier attempt, a repeat submit lands on the same checkout, and the account
    /// comes into being with a payment to attach to.
    ///
    /// Returns a failure only when the email belongs to an account that can sign in (has a
    /// password, or holds a staff role) — that person should buy from their account, not attach a
    /// payment to it from outside.
    /// </summary>
    /// <param name="successUrl">Must contain the provider's session-id placeholder — see the ticket flow.</param>
    /// <param name="cancelUrlTemplate">A template with <c>{code}</c> where the pending join's code
    /// goes, because the row does not exist yet when the caller builds the URL.</param>
    Task<MembershipCheckoutResult> InitiateJoinCheckoutAsync(JoinRequest request, string successUrl, string cancelUrlTemplate, CancellationToken ct = default);

    /// <summary>The pending join behind a resume code, or null. Read-only; for the resume page.</summary>
    Task<PendingJoinInfo?> GetPendingJoinByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// Opens a fresh checkout for an existing pending join whose session has lapsed (or reuses the
    /// live one). Fails if the row has already paid.
    /// </summary>
    Task<MembershipCheckoutResult> ResumeJoinCheckoutAsync(string code, string successUrl, string cancelUrlTemplate, CancellationToken ct = default);

    /// <summary>Reads LOCAL state only, same "never trust the browser redirect alone" rule as the ticket-purchase flow.</summary>
    Task<MembershipConfirmationInfo?> GetConfirmationBySessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>
    /// Clears the free-text and address fields from pending joins that never paid and have sat
    /// untouched for <paramref name="olderThan"/>. Returns how many rows were cleared. The row
    /// itself stays, so a very late payment on it can still be matched.
    /// </summary>
    Task<int> PurgeStalePendingJoinsAsync(TimeSpan olderThan, CancellationToken ct = default);

    /// <summary>
    /// Deliberately does NOT touch the shared ProcessedWebhookEvent ledger that PaymentService uses —
    /// idempotency here comes purely from MembershipPayment.Status (and, for renewals, from the
    /// expiry date only ever moving forward), so this is safe to call unconditionally alongside
    /// PaymentService's own webhook handling with zero risk of the two interfering with each
    /// other's idempotency bookkeeping.
    /// </summary>
    Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default);
}

public record MembershipCheckoutResult(bool Success, string? CheckoutUrl, string? Error)
{
    public static MembershipCheckoutResult Ok(string url) => new(true, url, null);
    public static MembershipCheckoutResult Fail(string error) => new(false, null, error);

    /// <summary>True when the failure is the plan being at its member limit — the page offers
    /// the waitlist rather than an error line.</summary>
    public bool PlanFull { get; init; }

    public static MembershipCheckoutResult Full(string error) => new(false, null, error) { PlanFull = true };
}

/// <param name="Max">The plan's limit; null when unlimited.</param>
/// <param name="Taken">Current members plus seats held by checkouts in progress.</param>
public record PlanAvailability(int? Max, int Taken)
{
    public int? Remaining => Max is { } max ? Math.Max(0, max - Taken) : null;
    public bool IsFull => Max is { } max && Taken >= max;
}

public record MembershipConfirmationInfo(bool IsConfirmed, string? PlanName, long AmountMinor, string Currency, DateTimeOffset? ExpiresAt)
{
    /// <summary>Set once the payment is confirmed, so the success page can hand the new member
    /// straight into onboarding without asking them to log in first (they have no password yet).</summary>
    public Guid? UserId { get; init; }
}

/// <summary>What the resume page shows. Paid means the checkout already went through — the page
/// should send the visitor to the welcome page for <c>PaidSessionId</c> instead of offering to pay.</summary>
public record PendingJoinInfo(string Code, string FirstName, string PlanName, bool IsPaid, string? PaidSessionId);

/// <summary>
/// What /join collects. The profile fields are the same set the application form and the account
/// profile ask for (see Profile), so a member who joined directly and one who was approved through
/// an application end up with the same record.
/// </summary>
public record JoinRequest(
    Guid PlanId,
    string FirstName,
    string LastName,
    string Email,
    string Country,
    string? City,
    string? ReferralCode)
{
    public string? JobTitle { get; init; }
    public string? AddressLine1 { get; init; }
    public string? AddressLine2 { get; init; }
    public string? PostalCode { get; init; }
    public string? About { get; init; }
    public string? Expectations { get; init; }
    public string? EarningsBand { get; init; }

    /// <summary>Where the terms were accepted from — recorded on the ConsentRecord written when
    /// the account is created.</summary>
    public string? IpAddress { get; init; }

    /// <summary>A promo code typed on the form; validated and redeemed when checkout opens.</summary>
    public string? PromoCode { get; init; }
}

/// <summary>The doors a tier opens, for one member. See MembershipPlan's entitlement flags.</summary>
public record MemberEntitlements(Guid PlanId, string PlanName, bool Community, bool Sessions, bool Directory, bool MemberCard, string? DiscordRoleId)
{
    public static MemberEntitlements From(MembershipPlan plan) =>
        new(plan.Id, plan.Name, plan.IncludesCommunity, plan.IncludesSessions, plan.IncludesDirectory, plan.IncludesMemberCard, plan.DiscordRoleId);
}

/// <summary>A membership row resolved against its plan, ready to display.</summary>
public record MembershipSummary(Membership Membership, MembershipPlan Plan)
{
    public MemberEntitlements Entitlements => MemberEntitlements.From(Plan);

    public bool IsRecurring => Plan.BillingPeriod != MembershipBillingPeriod.OneTime;

    /// <summary>True while the membership is in good standing — Active, or PastDue with the paid
    /// period not yet over — and, if it expires at all, has not yet. A PastDue member keeps access
    /// to the end of what they paid for; the provider's retries decide what happens after.</summary>
    public bool IsCurrent => (Membership.Status is MembershipStatus.Active or MembershipStatus.PastDue)
        && (Membership.ExpiresAt is null || Membership.ExpiresAt > DateTimeOffset.UtcNow);

    public bool IsPastDue => Membership.Status == MembershipStatus.PastDue;

    public bool IsComplimentary => Membership.IsComplimentary;

    /// <summary>True when the provider holds a live subscription for this row — what makes
    /// "manage billing" and "renews on" meaningful. PastDue counts: that is exactly the member who
    /// needs the billing page.</summary>
    public bool HasProviderSubscription => Membership.ProviderSubscriptionId is not null
        && (Membership.Status is MembershipStatus.Active or MembershipStatus.PastDue);
}

/// <summary>Outcome of an admin plan action. <c>Message</c> is a sentence for the status bar.</summary>
public record PlanMutationResult(bool Success, string Message)
{
    public static PlanMutationResult Ok(string message) => new(true, message);
    public static PlanMutationResult Fail(string message) => new(false, message);
}

public record PlanSyncSummary(int Synced, int Failed, List<string> Errors);

public record PlanImportSummary(int Imported, int Skipped, string? Error);

public record PlanUsage(int Memberships, int Payments)
{
    public bool CanDelete => Memberships == 0 && Payments == 0;
}
