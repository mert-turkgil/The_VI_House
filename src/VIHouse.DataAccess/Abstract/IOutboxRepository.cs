using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IOutboxRepository : IRepository<OutboxMessage>
{
    Task<bool> ExistsAsync(string dedupeKey, CancellationToken ct = default);

    /// <summary>The next batch the processor should deliver: undelivered, due, oldest first.</summary>
    Task<List<OutboxMessage>> GetDueAsync(DateTimeOffset now, int take, int maxAttempts, CancellationToken ct = default);
}
