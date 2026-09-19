using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IPaymentTransactionRepository : IRepository<PaymentTransaction>
{
    Task<PaymentTransaction?> GetBySessionAsync(string sessionId, CancellationToken ct = default);
    Task<PaymentTransaction?> GetByPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default);
    Task<PaymentTransaction?> GetByInvoiceAsync(string invoiceId, CancellationToken ct = default);
    Task<PaymentTransaction?> GetLatestForRelatedAsync(string relatedEntityType, Guid relatedEntityId, CancellationToken ct = default);

    /// <summary>
    /// The atomic step every status change goes through: one UPDATE guarded on the current status,
    /// so two events racing to move the same row can only both succeed if both moves are legal
    /// from where the row actually is. Returns false when the row was not in any of the
    /// <paramref name="from"/> states — the caller treats that as "already moved on".
    /// </summary>
    Task<bool> TryTransitionAsync(Guid id, IReadOnlyCollection<PaymentTransactionStatus> from, PaymentTransactionStatus to,
        DateTimeOffset now, string? eventId, CancellationToken ct = default);
}
