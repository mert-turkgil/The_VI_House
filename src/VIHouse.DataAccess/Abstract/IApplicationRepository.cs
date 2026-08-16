using VIHouse.Entities.Applications;

namespace VIHouse.DataAccess.Abstract;

public interface IApplicationRepository : IRepository<Application>
{
    Task<List<Application>> GetByStatusAsync(ApplicationStatus status, CancellationToken ct = default);
    Task<List<Application>> GetByExperienceAsync(Guid experienceId, CancellationToken ct = default);
    Task<Application?> GetWithTagsAsync(Guid id, CancellationToken ct = default);
}
