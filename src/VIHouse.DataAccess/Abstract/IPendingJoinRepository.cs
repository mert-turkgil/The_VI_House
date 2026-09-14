using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Abstract;

public interface IPendingJoinRepository : IRepository<PendingJoin>
{
    Task<PendingJoin?> GetByCodeAsync(string code, CancellationToken ct = default);

    Task<PendingJoin?> GetBySessionAsync(string providerSessionId, CancellationToken ct = default);

    /// <summary>Newest Pending or Expired row for the address, or null. Paid and Superseded rows are
    /// never returned — a retry must not resurrect either.</summary>
    Task<PendingJoin?> GetLatestOpenByEmailAsync(string emailNormalized, CancellationToken ct = default);

    /// <summary>Every row for the address in one of <paramref name="statuses"/>, excluding <paramref name="exceptId"/>.</summary>
    Task<IReadOnlyList<PendingJoin>> ListByEmailAsync(string emailNormalized, PendingJoinStatus[] statuses, Guid? exceptId = null, CancellationToken ct = default);

    Task<bool> AnyPaidForEmailAsync(string emailNormalized, CancellationToken ct = default);

    /// <summary>
    /// Atomically moves the row from any of <paramref name="from"/> to <paramref name="to"/>, and
    /// says whether this call was the one that did it. Same no-double-spend shape as
    /// <see cref="IPromoCodeRepository.TryRedeemAsync"/>: the webhook that wins this claim is the
    /// one that provisions; a redelivery gets false and does nothing.
    ///
    /// Executes immediately against the database, bypassing the change tracker — call it before
    /// loading the entity, or a later SaveChangesAsync writes the stale in-memory Status back.
    /// </summary>
    Task<bool> TryClaimAsync(Guid id, PendingJoinStatus[] from, PendingJoinStatus to, CancellationToken ct = default);

    /// <summary>Expired/Superseded rows last touched before <paramref name="olderThan"/> that still
    /// hold their form data — the purge sweep's work list.</summary>
    Task<IReadOnlyList<PendingJoin>> ListPurgeableAsync(DateTimeOffset olderThan, int take, CancellationToken ct = default);
}
