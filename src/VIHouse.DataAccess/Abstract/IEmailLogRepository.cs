using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Abstract;

public interface IEmailLogRepository : IRepository<EmailLog>
{
    /// <summary>
    /// The most recent attempts, newest first, optionally narrowed to one status.
    ///
    /// Paged rather than GetAllAsync because this table only ever grows: every approval, every
    /// booking confirmation and every password reset writes a row, and an admin screen that loads
    /// all of them gets slower every week it runs.
    /// </summary>
    Task<List<EmailLog>> GetRecentAsync(EmailStatus? status, int skip, int take, CancellationToken ct = default);

    /// <summary>Total matching <paramref name="status"/>, for the pager and the failure count.</summary>
    Task<int> CountAsync(EmailStatus? status, CancellationToken ct = default);

    /// <summary>Every attempt tied to one record. The application review screen answers "did their
    /// payment link go out" with this, without sending the admin to the full log to find out.</summary>
    Task<List<EmailLog>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
}
