using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfPaymentTransactionRepository(VIHouseDbContext db) : EfRepository<PaymentTransaction>(db), IPaymentTransactionRepository
{
    public Task<PaymentTransaction?> GetBySessionAsync(string sessionId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(t => t.ProviderSessionId == sessionId, ct);

    public Task<PaymentTransaction?> GetByPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(t => t.ProviderPaymentIntentId == paymentIntentId, ct);

    public Task<PaymentTransaction?> GetByInvoiceAsync(string invoiceId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(t => t.ProviderInvoiceId == invoiceId, ct);

    public Task<PaymentTransaction?> GetLatestForRelatedAsync(string relatedEntityType, Guid relatedEntityId, CancellationToken ct = default) =>
        Set.Where(t => t.RelatedEntityType == relatedEntityType && t.RelatedEntityId == relatedEntityId)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> TryTransitionAsync(Guid id, IReadOnlyCollection<PaymentTransactionStatus> from, PaymentTransactionStatus to,
        DateTimeOffset now, string? eventId, CancellationToken ct = default)
    {
        var affected = await Set
            .Where(t => t.Id == id && from.Contains(t.Status))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, to)
                .SetProperty(t => t.UpdatedAt, now)
                .SetProperty(t => t.LastEventId, eventId)
                .SetProperty(t => t.PaidAt, t => to == PaymentTransactionStatus.Succeeded ? now : t.PaidAt)
                .SetProperty(t => t.FailedAt, t => to == PaymentTransactionStatus.Failed ? now : t.FailedAt)
                .SetProperty(t => t.CanceledAt, t => to == PaymentTransactionStatus.Canceled ? now : t.CanceledAt)
                .SetProperty(t => t.ExpiredAt, t => to == PaymentTransactionStatus.Expired ? now : t.ExpiredAt)
                .SetProperty(t => t.RefundedAt, t => to == PaymentTransactionStatus.Refunded || to == PaymentTransactionStatus.PartiallyRefunded ? now : t.RefundedAt), ct);

        if (affected != 1) return false;

        // Keep a tracked copy, if any, honest about what just happened on the database side.
        var tracked = Db.ChangeTracker.Entries<PaymentTransaction>().FirstOrDefault(e => e.Entity.Id == id);
        if (tracked is not null)
        {
            await tracked.ReloadAsync(ct);
        }
        return true;
    }
}
