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

    /// <summary>Null when the plan has no limit.</summary>
    public int? MaxMembers { get; set; }
    public int? Remaining { get; set; }
    public bool IsFull { get; set; }

    /// <summary>Scarcity only when it is real — the same rule the ticket panel applies.</summary>
    public bool ShowRemaining => Remaining is > 0 and <= 10;

    public static MembershipPlanCardViewModel FromEntity(MembershipPlan p, VIHouse.Business.Abstract.PlanAvailability? availability = null) => new()
    {
        MaxMembers = p.MaxMembers,
        Remaining = availability?.Remaining,
        IsFull = availability?.IsFull ?? false,
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
