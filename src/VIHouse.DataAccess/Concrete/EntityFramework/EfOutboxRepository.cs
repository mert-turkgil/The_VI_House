using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfOutboxRepository(VIHouseDbContext db) : EfRepository<OutboxMessage>(db), IOutboxRepository
{
    public Task<bool> ExistsAsync(string dedupeKey, CancellationToken ct = default) =>
        Set.AnyAsync(m => m.DedupeKey == dedupeKey, ct);

    public Task<List<OutboxMessage>> GetDueAsync(DateTimeOffset now, int take, int maxAttempts, CancellationToken ct = default) =>
        Set.Where(m => m.ProcessedAt == null && m.NextAttemptAt <= now && m.Attempts < maxAttempts)
            .OrderBy(m => m.NextAttemptAt)
            .Take(take)
            .ToListAsync(ct);
}
