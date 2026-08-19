using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Notifications;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfNotificationRepository(VIHouseDbContext db) : EfRepository<Notification>(db), INotificationRepository
{
    public Task<List<Notification>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        Set.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).ToListAsync(ct);

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        Set.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
}
