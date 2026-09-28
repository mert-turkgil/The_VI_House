using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Common;
using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

/// <summary>The one implementation behind both message logs.</summary>
public abstract class EfMessageLogRepository<T>(VIHouseDbContext db) : EfRepository<T>(db), IMessageLogRepository<T>
    where T : BaseEntity, IMessageLog
{
    public Task<List<T>> GetRecentAsync(EmailStatus? status, int skip, int take, CancellationToken ct = default) =>
        Filter(status)
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountAsync(EmailStatus? status, CancellationToken ct = default) =>
        Filter(status).CountAsync(ct);

    public Task<List<T>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken ct = default) =>
        Set
            .AsNoTracking()
            .Where(e => e.RelatedEntityType == entityType && e.RelatedEntityId == entityId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

    private IQueryable<T> Filter(EmailStatus? status) =>
        status is null ? Set : Set.Where(e => e.Status == status);
}
