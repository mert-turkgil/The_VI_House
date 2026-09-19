using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfWebhookEventRepository(VIHouseDbContext db) : IWebhookEventRepository
{
    public Task<WebhookEvent?> GetAsync(string eventId, CancellationToken ct = default) =>
        db.WebhookEvents.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId, ct);

    public async Task<bool> TryInsertAsync(WebhookEvent webhookEvent, CancellationToken ct = default)
    {
        // The primary key is the gate. Two deliveries of one event racing each other both reach
        // this insert; exactly one wins, the other sees the key violation and backs off. Detached
        // afterwards either way, so a later rollback of the handler transaction cannot un-track it.
        var entry = await db.WebhookEvents.AddAsync(webhookEvent, ct);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            return false;
        }
        finally
        {
            entry.State = EntityState.Detached;
        }
    }

    public async Task<bool> TryStartAttemptAsync(string eventId, IReadOnlyCollection<WebhookEventStatus> from, DateTimeOffset now, CancellationToken ct = default)
    {
        var affected = await db.WebhookEvents
            .Where(e => e.EventId == eventId && from.Contains(e.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, WebhookEventStatus.Received)
                .SetProperty(e => e.Attempts, e => e.Attempts + 1)
                .SetProperty(e => e.LastAttemptAt, now), ct);
        return affected == 1;
    }

    public Task MarkProcessedAsync(string eventId, DateTimeOffset now, CancellationToken ct = default) =>
        db.WebhookEvents.Where(e => e.EventId == eventId).ExecuteUpdateAsync(s => s
            .SetProperty(e => e.Status, WebhookEventStatus.Processed)
            .SetProperty(e => e.ProcessedAt, now)
            .SetProperty(e => e.LastError, (string?)null), ct);

    public Task MarkIgnoredAsync(string eventId, DateTimeOffset now, CancellationToken ct = default) =>
        db.WebhookEvents.Where(e => e.EventId == eventId).ExecuteUpdateAsync(s => s
            .SetProperty(e => e.Status, WebhookEventStatus.Ignored)
            .SetProperty(e => e.ProcessedAt, now)
            .SetProperty(e => e.LastError, (string?)null), ct);

    public Task MarkFailedAsync(string eventId, string error, DateTimeOffset now, CancellationToken ct = default)
    {
        var clipped = error.Length > 2000 ? error[..2000] : error;
        return db.WebhookEvents.Where(e => e.EventId == eventId).ExecuteUpdateAsync(s => s
            .SetProperty(e => e.Status, WebhookEventStatus.Failed)
            .SetProperty(e => e.LastAttemptAt, now)
            .SetProperty(e => e.LastError, clipped), ct);
    }

    public Task<List<WebhookEvent>> ListAsync(WebhookEventStatus? status, int take, CancellationToken ct = default)
    {
        IQueryable<WebhookEvent> query = db.WebhookEvents.AsNoTracking();
        if (status is { } s) query = query.Where(e => e.Status == s);
        return query.OrderByDescending(e => e.ReceivedAt).Take(take).ToListAsync(ct);
    }

    public async Task<Dictionary<WebhookEventStatus, int>> CountByStatusAsync(CancellationToken ct = default) =>
        await db.WebhookEvents.GroupBy(e => e.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

    /// <summary>SQL Server's unique-key violations: 2627 (PK/unique constraint), 2601 (unique index).</summary>
    private static bool IsDuplicateKey(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2627 or 2601 };
}
