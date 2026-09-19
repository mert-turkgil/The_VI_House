using Microsoft.EntityFrameworkCore.Storage;
using VIHouse.DataAccess.Abstract;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfUnitOfWork(VIHouseDbContext db) : IUnitOfWork
{
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default) =>
        new EfTransaction(await db.Database.BeginTransactionAsync(ct));

    public void ClearTracking() => db.ChangeTracker.Clear();

    private sealed class EfTransaction(IDbContextTransaction inner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken ct = default) => inner.CommitAsync(ct);
        public Task RollbackAsync(CancellationToken ct = default) => inner.RollbackAsync(ct);
        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}
