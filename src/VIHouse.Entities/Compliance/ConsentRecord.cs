using VIHouse.Entities.Common;

namespace VIHouse.Entities.Compliance;

/// <summary>Minimal GDPR consent scaffolding. Text snapshots exactly what copy the user agreed to, at that point in time.</summary>
public class ConsentRecord : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid? ApplicationId { get; set; }
    public ConsentType Type { get; set; }
    public bool Granted { get; set; }
    public string Text { get; set; } = default!;
    public DateTimeOffset GrantedAt { get; set; }
    public string? IpAddress { get; set; }
}
