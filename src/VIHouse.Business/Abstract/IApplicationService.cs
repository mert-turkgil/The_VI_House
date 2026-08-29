using VIHouse.Entities.Applications;

namespace VIHouse.Business.Abstract;

public interface IApplicationService
{
    /// <summary>Public entry point: creates the Application already in Submitted state (brief §25-26) and, if <paramref name="agreedToTerms"/>, logs a TermsOfService consent record.</summary>
    Task<Application> SubmitAsync(Application application, bool agreedToTerms, string? ipAddress, CancellationToken ct = default);

    Task<List<Application>> GetByStatusAsync(ApplicationStatus? status, CancellationToken ct = default);
    Task<Application?> GetForAdminAsync(Guid id, CancellationToken ct = default);

    Task MarkUnderReviewAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task ShortlistAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Shortlisted (or Waitlisted) -> Approved. Issues a 14-day single-use Invitation and sends the
    /// private payment link by email and text message, all in one save (brief §28-29).
    /// </summary>
    Task<InvitationDeliveryResult> ApproveAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Sends the payment link again — for the applicant who deleted the email, changed address, or
    /// let it run out. An expired link is replaced with a fresh one rather than resent; a link that
    /// has already been paid with is refused.
    /// </summary>
    Task<InvitationDeliveryResult> ResendInvitationAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task RejectAsync(Guid id, Guid adminUserId, string? reason, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Shortlisted -> Waitlisted. Not a verdict: a waitlisted application can still be approved or
    /// rejected later, which is the whole point of the state (see ApplicationService's transition table).
    /// </summary>
    Task WaitlistAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- System-triggered (Stripe checkout flow, not an admin action — see PaymentService) ---
    Task MarkPaymentPendingAsync(Guid id, CancellationToken ct = default);
    Task MarkPaidAsync(Guid id, CancellationToken ct = default);
    Task RevertToApprovedAsync(Guid id, CancellationToken ct = default);

    Task UpdateInternalNotesAsync(Guid id, string? notes, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task AddTagAsync(Guid applicationId, string label, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveTagAsync(Guid applicationId, Guid tagId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}

/// <summary>
/// What happened when the payment link was sent, in words an admin can act on.
/// </summary>
/// <param name="Sent">True if it reached the applicant by at least one channel. False means they have
/// no way to pay yet, which is worth noticing before the seat is counted as filled.</param>
public record InvitationDeliveryResult(bool Sent, string Message);
