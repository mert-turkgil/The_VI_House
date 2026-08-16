using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

public class PromoCode : BaseEntity
{
    public string Code { get; set; } = default!;
    public PromoCodeType Type { get; set; }

    /// <summary>Percentage 0-100 when Type == Percentage; minor-units amount when Type == Fixed.</summary>
    public int Value { get; set; }
    public string? Currency { get; set; }

    public Guid? ExperienceId { get; set; }
    public Guid? RestrictedToUserId { get; set; }
    public int? MaxRedemptions { get; set; }
    public int RedemptionCount { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
}
