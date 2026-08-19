using VIHouse.Entities.Notifications;

namespace VIHouse.DataAccess.Abstract;

public interface INotificationRepository : IRepository<Notification>
{
    Task<List<Notification>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
}
