using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfAuditLogRepository(VIHouseDbContext db) : EfRepository<AuditLogEntry>(db), IAuditLogRepository
{
}
