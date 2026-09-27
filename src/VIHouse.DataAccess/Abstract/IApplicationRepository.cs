using VIHouse.Entities.Applications;

namespace VIHouse.DataAccess.Abstract;

public interface IApplicationRepository : IRepository<Application>
{
    Task<List<Application>> GetByStatusAsync(ApplicationStatus status, CancellationToken ct = default);
    Task<List<Application>> GetByExperienceAsync(Guid experienceId, CancellationToken ct = default);
    Task<Application?> GetWithTagsAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The applications one person may see the status of: those whose ids their browser holds (the
    /// status cookie), plus — when signed in — those linked to their account or sent from its email
    /// address. Newest first. The email is compared under a Latin collation: the database's Turkish
    /// one treats "I" and "i" as different letters, so "Mert@..." would otherwise miss "mert@...".
    /// </summary>
    Task<List<Application>> GetForApplicantAsync(IReadOnlyCollection<Guid> ids, Guid? userId, string? email, CancellationToken ct = default);
}
