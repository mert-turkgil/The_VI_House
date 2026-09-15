using VIHouse.Entities.Common;

namespace VIHouse.Entities.Membership;

/// <summary>
/// Brief §44-45: membership tiers (Applicant/Guest/Member/Founding Member/Partner/VIP...) are data,
/// not hard-coded — the tier IS the plan's Name, admin-managed, not a C# enum.
///
/// Each plan is mirrored to the payment provider's catalogue as one Product carrying one active
/// Price (see IPaymentCatalogProvider). The local row is the source of truth: an admin edits it
/// here and the mirror is pushed, never the other way round — except for an explicit import, which
/// only ever creates rows that do not exist yet. The Provider* columns are the bookkeeping for that
/// mirror, and are the only place a provider id is stored for a plan.
/// </summary>
public class MembershipPlan : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public long PriceMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public MembershipBillingPeriod BillingPeriod { get; set; } = MembershipBillingPeriod.Annual;

    /// <summary>Plain text, one feature per line — matches the brief's flat "Features" field; no separate feature-list entity for this MVP.</summary>
    public string? Features { get; set; }

    public MembershipPlanStatus Status { get; set; } = MembershipPlanStatus.Active;
    public int SortOrder { get; set; }

    /// <summary>
    /// How many people may hold this plan at once; null is unlimited. Counted against current
    /// memberships plus seats in checkout (see IMembershipService.GetPlanAvailabilityAsync), and
    /// enforced when a checkout is opened — never in the webhook, which must not refuse money.
    /// </summary>
    public int? MaxMembers { get; set; }

    /// <summary>The provider's Product id ("prod_…" at Stripe). Null until the first successful sync.</summary>
    public string? ProviderProductId { get; set; }

    /// <summary>
    /// The provider's currently active Price id ("price_…"). Prices are immutable at Stripe, so a
    /// change to <see cref="PriceMinor"/>, <see cref="Currency"/> or <see cref="BillingPeriod"/>
    /// creates a new one and retires the old; this always points at the one checkout should use.
    /// </summary>
    public string? ProviderPriceId { get; set; }

    /// <summary>When the mirror last matched this row. Null means never synced — or synced, then
    /// edited, and the push that followed failed (see <see cref="ProviderSyncError"/>).</summary>
    public DateTimeOffset? ProviderSyncedAt { get; set; }

    /// <summary>The last sync failure, kept so the admin screen can say why rather than just "not
    /// synced". Cleared by the next successful push.</summary>
    public string? ProviderSyncError { get; set; }

    /// <summary>True when checkout can reference the provider's own Price rather than inlining the
    /// amount — i.e. the mirror exists and is not known to be stale.</summary>
    public bool IsProviderSynced => ProviderPriceId is not null && ProviderSyncedAt is not null && ProviderSyncError is null;
}
