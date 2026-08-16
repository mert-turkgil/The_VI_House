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

    /// <summary>Shortlisted -> Approved. Issues a 14-day single-use Invitation and queues (does not send — no email pipeline yet) the approval email, all in one save (brief §28-29).</summary>
    Task ApproveAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task RejectAsync(Guid id, Guid adminUserId, string? reason, string? ipAddress, CancellationToken ct = default);
    Task WaitlistAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task UpdateInternalNotesAsync(Guid id, string? notes, CancellationToken ct = default);
    Task AddTagAsync(Guid applicationId, string label, CancellationToken ct = default);
    Task RemoveTagAsync(Guid applicationId, Guid tagId, CancellationToken ct = default);
}
