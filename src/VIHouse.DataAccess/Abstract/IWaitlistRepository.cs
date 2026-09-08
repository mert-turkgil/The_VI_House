using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IWaitlistRepository : IRepository<WaitlistEntry>
{
    Task<List<WaitlistEntry>> GetByExperienceOrderedAsync(Guid experienceId, CancellationToken ct = default);
    Task<int> GetNextPositionAsync(Guid experienceId, CancellationToken ct = default);

    /// <summary>
    /// The entry this person already holds, or null. Lets a repeat sign-up be answered with the
    /// place they are already in rather than refused — see ExperienceService.JoinWaitlistAsync.
    /// </summary>
    Task<WaitlistEntry?> FindByEmailAsync(Guid experienceId, string email, CancellationToken ct = default);
}
