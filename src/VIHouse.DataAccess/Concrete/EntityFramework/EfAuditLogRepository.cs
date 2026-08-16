using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfAuditLogRepository(VIHouseDbContext db) : EfRepository<AuditLogEntry>(db), IAuditLogRepository
{
    public Task<List<AuditLogEntry>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default) =>
        Set.Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
}
