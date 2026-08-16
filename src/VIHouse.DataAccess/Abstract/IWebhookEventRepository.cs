namespace VIHouse.DataAccess.Abstract;

/// <summary>Idempotency ledger access for inbound payment-provider webhooks (brief §32).</summary>
public interface IWebhookEventRepository
{
    Task<bool> HasBeenProcessedAsync(string eventId, CancellationToken ct = default);
    Task MarkProcessedAsync(string eventId, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
