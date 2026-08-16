using System.Linq.Expressions;
using VIHouse.Entities.Common;

namespace VIHouse.DataAccess.Abstract;

/// <summary>Generic CRUD contract shared by every entity-specific repository below.</summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);

    /// <summary>Commits pending changes on the shared DbContext. Call once per unit of work, not per repository call.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
