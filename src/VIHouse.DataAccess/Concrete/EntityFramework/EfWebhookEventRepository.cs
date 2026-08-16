using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfWebhookEventRepository(VIHouseDbContext db) : IWebhookEventRepository
{
    public Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default) =>
        db.ProcessedWebhookEvents.AnyAsync(e => e.EventId == eventId, ct);

    public async Task MarkProcessedAsync(string eventId, CancellationToken ct = default) =>
        await db.ProcessedWebhookEvents.AddAsync(new ProcessedWebhookEvent { EventId = eventId }, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
