using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfSmsLogRepository(VIHouseDbContext db) : EfRepository<SmsLog>(db), ISmsLogRepository
{
    public async Task<List<SmsLog>> GetRecentAsync(EmailStatus? status, int skip, int take, CancellationToken ct = default) =>
        await Filter(status)
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountAsync(EmailStatus? status, CancellationToken ct = default) =>
        Filter(status).CountAsync(ct);

    public async Task<List<SmsLog>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken ct = default) =>
        await Set
            .AsNoTracking()
            .Where(e => e.RelatedEntityType == entityType && e.RelatedEntityId == entityId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

    private IQueryable<SmsLog> Filter(EmailStatus? status) =>
        status is null ? Set : Set.Where(e => e.Status == status);
}
