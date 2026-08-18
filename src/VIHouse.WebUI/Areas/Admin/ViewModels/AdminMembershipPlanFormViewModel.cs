using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Membership;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminMembershipPlanFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = default!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required, Range(0, long.MaxValue)]
    public long PriceMinor { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "GBP";

    [Required]
    public MembershipBillingPeriod BillingPeriod { get; set; } = MembershipBillingPeriod.Annual;

    [Display(Name = "Features (one per line)")]
    [StringLength(2000)]
    public string? Features { get; set; }

    [Required]
    public MembershipPlanStatus Status { get; set; } = MembershipPlanStatus.Active;

    public int SortOrder { get; set; }

    public MembershipPlan ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Name = Name.Trim(),
        Description = Description,
        PriceMinor = PriceMinor,
        Currency = Currency.Trim().ToUpperInvariant(),
        BillingPeriod = BillingPeriod,
        Features = Features,
        Status = Status,
        SortOrder = SortOrder,
    };

    public static AdminMembershipPlanFormViewModel FromEntity(MembershipPlan p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        PriceMinor = p.PriceMinor,
        Currency = p.Currency,
        BillingPeriod = p.BillingPeriod,
        Features = p.Features,
        Status = p.Status,
        SortOrder = p.SortOrder,
    };
}
