using VIHouse.Entities.Common;
using VIHouse.Entities.Communication;

namespace VIHouse.DataAccess.Abstract;

/// <summary>The queries the email and the SMS log share (Admin > Emails &amp; SMS, and the
/// application review screen's "did their link go out").</summary>
public interface IMessageLogRepository<T> : IRepository<T> where T : BaseEntity, IMessageLog
{
    /// <summary>
    /// The most recent attempts, newest first, optionally narrowed to one status. Paged rather than
    /// GetAllAsync because these tables only ever grow: every approval, confirmation and password
    /// reset writes a row.
    /// </summary>
    Task<List<T>> GetRecentAsync(EmailStatus? status, int skip, int take, CancellationToken ct = default);

    /// <summary>Total matching <paramref name="status"/>, for the pager and the failure count.</summary>
    Task<int> CountAsync(EmailStatus? status, CancellationToken ct = default);

    /// <summary>Every attempt tied to one record.</summary>
    Task<List<T>> GetForEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);
}
