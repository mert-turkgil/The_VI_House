namespace VIHouse.Business.Abstract;

/// <summary>
/// The payment provider's product catalogue, behind the same provider-agnostic wall as
/// <see cref="IPaymentProvider"/>: no Stripe.net type crosses this interface. Where
/// IPaymentProvider takes money, this one keeps the provider's idea of "what is for sale" in step
/// with ours — one Product carrying one active Price per <c>MembershipPlan</c>.
///
/// Direction is deliberately one-way. The local plan row is the source of truth and every call
/// here pushes it outwards; <see cref="ListPlansAsync"/> exists only so an admin can pull in
/// products that were created at the provider first, and MembershipService only ever uses it to
/// create rows that do not exist yet — never to overwrite ones that do.
///
/// Every method throws on failure (network, bad key, provider rejection). The caller catches,
/// records the message on the plan, and carries on: a plan that failed to mirror is still a plan,
/// and checkout falls back to an inline amount for it until the next sync succeeds.
/// </summary>
public interface IPaymentCatalogProvider
{
    /// <summary>
    /// Creates the mirror on the first call and updates it on every one after. Name, description
    /// and active state are updated in place; a change to amount, currency or interval issues a
    /// new Price (prices are immutable at Stripe) and retires the previous one. The returned ids
    /// are what the plan must store — the Price id in particular may differ from the one passed in.
    /// </summary>
    Task<CatalogSyncResult> SyncPlanAsync(CatalogPlan plan, CancellationToken ct = default);

    /// <summary>
    /// Takes the mirror off sale — product and price both deactivated — without deleting either.
    /// Used for a local archive and for a local delete alike: a product that has ever carried a
    /// price cannot be deleted at Stripe, and a subscription still running against it must keep
    /// resolving, so "delete" at the provider is always "archive".
    /// </summary>
    Task ArchivePlanAsync(string productId, string? priceId, CancellationToken ct = default);

    /// <summary>
    /// Every active product at the provider whose default price is a plain one-off or
    /// month/year recurring amount — the only shapes a MembershipPlan can represent. Tiered,
    /// metered and weekly/daily prices are skipped rather than mis-imported.
    /// </summary>
    Task<List<CatalogPlan>> ListPlansAsync(CancellationToken ct = default);
}

/// <summary>
/// A plan as the catalogue sees it. <paramref name="LocalPlanId"/> rides along as provider
/// metadata so a product can be matched back to its row on import even if the ids here were lost.
/// </summary>
public record CatalogPlan(
    string? ProductId,
    string? PriceId,
    Guid? LocalPlanId,
    string Name,
    string? Description,
    long AmountMinor,
    string Currency,
    RecurringInterval? Recurring,
    bool Active);

/// <param name="PriceReplaced">True when a new Price was issued because the amount, currency or
/// interval changed — worth surfacing to the admin, since the old price stays visible in the
/// provider's dashboard as archived.</param>
public record CatalogSyncResult(string ProductId, string PriceId, bool PriceReplaced);
