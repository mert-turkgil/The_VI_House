using VIHouse.Entities.Users;

namespace VIHouse.DataAccess.Abstract;

/// <summary>
/// Not IRepository&lt;T&gt;-based: Profile is keyed by UserId, not a BaseEntity.Id, since it's a 1:1
/// shadow of the Identity user rather than an independent aggregate.
/// </summary>
public interface IProfileRepository
{
    Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(Profile profile, CancellationToken ct = default);
    void Update(Profile profile);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
