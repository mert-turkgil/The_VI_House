using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfPendingJoinRepository(VIHouseDbContext db) : EfRepository<PendingJoin>(db), IPendingJoinRepository
{
    private static readonly PendingJoinStatus[] OpenStatuses = [PendingJoinStatus.Pending, PendingJoinStatus.Expired];

    public Task<PendingJoin?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.Code == code, ct);

    public Task<PendingJoin?> GetBySessionAsync(string providerSessionId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.ProviderSessionId == providerSessionId, ct);

    public Task<PendingJoin?> GetLatestOpenByEmailAsync(string emailNormalized, CancellationToken ct = default) =>
        Set.Where(p => p.EmailNormalized == emailNormalized && OpenStatuses.Contains(p.Status))
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PendingJoin>> ListByEmailAsync(string emailNormalized, PendingJoinStatus[] statuses, Guid? exceptId = null, CancellationToken ct = default) =>
        await Set.Where(p => p.EmailNormalized == emailNormalized && statuses.Contains(p.Status) && (exceptId == null || p.Id != exceptId))
            .ToListAsync(ct);

    public Task<bool> AnyPaidForEmailAsync(string emailNormalized, CancellationToken ct = default) =>
        Set.AnyAsync(p => p.EmailNormalized == emailNormalized && p.Status == PendingJoinStatus.Paid, ct);

    public async Task<bool> TryClaimAsync(Guid id, PendingJoinStatus[] from, PendingJoinStatus to, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var affected = await Set
            .Where(p => p.Id == id && from.Contains(p.Status))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, to)
                .SetProperty(p => p.UpdatedAt, now), ct);

        return affected == 1;
    }

    public async Task<IReadOnlyList<PendingJoin>> ListPurgeableAsync(DateTimeOffset olderThan, int take, CancellationToken ct = default) =>
        await Set.Where(p => p.PurgedAt == null
                             && (p.Status == PendingJoinStatus.Expired || p.Status == PendingJoinStatus.Superseded)
                             && (p.UpdatedAt ?? p.CreatedAt) < olderThan)
            .OrderBy(p => p.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
}
