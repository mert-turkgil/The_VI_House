using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfEmailLogRepository(VIHouseDbContext db) : EfRepository<EmailLog>(db), IEmailLogRepository
{
}
