using VIHouse.Entities.Audit;

namespace VIHouse.DataAccess.Abstract;

public interface IAuditLogRepository : IRepository<AuditLogEntry>
{
    Task<List<AuditLogEntry>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
}
