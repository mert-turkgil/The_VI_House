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

    /// <summary>Shortlisted -> Approved. Issues a 14-day single-use Invitation and sends the approval email, all in one save (brief §28-29).</summary>
    Task ApproveAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task RejectAsync(Guid id, Guid adminUserId, string? reason, string? ipAddress, CancellationToken ct = default);
    Task WaitlistAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- System-triggered (Stripe checkout flow, not an admin action — see PaymentService) ---
    Task MarkPaymentPendingAsync(Guid id, CancellationToken ct = default);
    Task MarkPaidAsync(Guid id, CancellationToken ct = default);
    Task RevertToApprovedAsync(Guid id, CancellationToken ct = default);

    Task UpdateInternalNotesAsync(Guid id, string? notes, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task AddTagAsync(Guid applicationId, string label, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveTagAsync(Guid applicationId, Guid tagId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}
