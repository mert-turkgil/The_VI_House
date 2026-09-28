using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfSmsLogRepository(VIHouseDbContext db) : EfMessageLogRepository<SmsLog>(db), ISmsLogRepository;
