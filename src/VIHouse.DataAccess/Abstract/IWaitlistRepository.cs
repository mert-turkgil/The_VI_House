using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IWaitlistRepository : IRepository<WaitlistEntry>
{
    Task<List<WaitlistEntry>> GetByExperienceOrderedAsync(Guid experienceId, CancellationToken ct = default);
    Task<int> GetNextPositionAsync(Guid experienceId, CancellationToken ct = default);
}
