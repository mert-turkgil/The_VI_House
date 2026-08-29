using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Abstract;

public interface ISmsLogRepository : IRepository<SmsLog>
{
    /// <summary>The most recent attempts, newest first, optionally narrowed to one status. Paged for
    /// the same reason as the email log: this table only ever grows.</summary>
    Task<List<SmsLog>> GetRecentAsync(EmailStatus? status, int skip, int take, CancellationToken ct = default);

    Task<int> CountAsync(EmailStatus? status, CancellationToken ct = default);

    /// <summary>Every attempt tied to one record. The application review screen answers "did their
    /// payment link go out, and where" with this.</summary>
    Task<List<SmsLog>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
}
