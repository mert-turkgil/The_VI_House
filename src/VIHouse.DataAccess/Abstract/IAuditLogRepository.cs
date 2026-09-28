using VIHouse.Entities.Audit;

namespace VIHouse.DataAccess.Abstract;

public interface IAuditLogRepository : IRepository<AuditLogEntry>
{
}
