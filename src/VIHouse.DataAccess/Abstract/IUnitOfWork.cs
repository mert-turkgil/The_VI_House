namespace VIHouse.DataAccess.Abstract;

/// <summary>
/// An explicit database transaction over the request's shared context, for the few places where
/// several repositories must commit together or not at all — a webhook that flips a payment to
/// paid and creates the booking is the reason this exists. Everything else keeps using the
/// repositories' implicit unit of work (one SaveChanges, one auto-commit).
/// </summary>
public interface IUnitOfWork
{
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);

    /// <summary>
    /// Forgets every entity the context is tracking. Required after a rollback: the tracker still
    /// holds the failed attempt's modified entities, and any later SaveChanges on the same context
    /// would flush them as if the rollback had never happened.
    /// </summary>
    void ClearTracking();
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
