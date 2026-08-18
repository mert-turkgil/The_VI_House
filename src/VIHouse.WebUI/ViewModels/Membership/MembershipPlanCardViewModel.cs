using VIHouse.Entities.Membership;

namespace VIHouse.WebUI.ViewModels.Membership;

public class MembershipPlanCardViewModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public long PriceMinor { get; set; }
    public string Currency { get; set; } = default!;
    public MembershipBillingPeriod BillingPeriod { get; set; }
    public List<string> Features { get; set; } = [];

    public static MembershipPlanCardViewModel FromEntity(MembershipPlan p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        PriceMinor = p.PriceMinor,
        Currency = p.Currency,
        BillingPeriod = p.BillingPeriod,
        Features = string.IsNullOrWhiteSpace(p.Features)
            ? []
            : p.Features.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
    };
}
