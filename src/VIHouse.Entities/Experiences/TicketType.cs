using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

/// <summary>
/// A purchasable tier on an Experience. Price is stored as integer minor units (e.g. pence/cents),
/// never float/decimal, per brief §184 — avoids floating-point rounding drift on money.
/// Inventory is the authoritative sellable-count; it is only ever mutated through
/// ICapacityService's atomic conditional UPDATE (see VIHouse.Business), never read-then-written
/// from application code, to prevent overselling under concurrent checkouts (brief §24).
/// </summary>
public class TicketType : BaseEntity
{
    public Guid ExperienceId { get; set; }

    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    public long PriceMinor { get; set; }
    public string Currency { get; set; } = "GBP";

    public int Inventory { get; set; }
    public int MaxQuantityPerOrder { get; set; } = 1;

    public DateTimeOffset? SalesStartAt { get; set; }
    public DateTimeOffset? SalesEndAt { get; set; }

    public string? PerksText { get; set; }
    public int SortOrder { get; set; }

    /// <summary>EF concurrency token — belt-and-braces alongside the atomic UPDATE in CapacityService.</summary>
    public byte[]? RowVersion { get; set; }
}
