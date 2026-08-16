using VIHouse.Entities.Common;

namespace VIHouse.Entities.Audit;

/// <summary>Every mutating admin action, for high-value-transaction accountability (brief §97).</summary>
public class AuditLogEntry : BaseEntity
{
    public Guid AdminUserId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public Guid? EntityId { get; set; }
    public string? DataBefore { get; set; }
    public string? DataAfter { get; set; }
    public string? IpAddress { get; set; }
}
