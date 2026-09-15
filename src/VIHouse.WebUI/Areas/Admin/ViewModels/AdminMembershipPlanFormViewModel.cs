using System.ComponentModel.DataAnnotations;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Membership;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminMembershipPlanFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = default!;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Entered as a decimal in major units (e.g. 2500.00) rather than the minor-unit integer the
    /// row stores. The old "250000 = £2,500.00" box was the single easiest way to sell a plan for
    /// a hundredth of its price.
    /// </summary>
    [Required, Range(0, 10_000_000)]
    [Display(Name = "Price")]
    public decimal Price { get; set; }

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

    /// <summary>Seat limit; blank means unlimited. Adjustable at any time — raising it reopens
    /// sales, lowering it below the current count stops new joins without touching anyone.</summary>
    [Range(1, 1_000_000)]
    public int? MaxMembers { get; set; }

    // --- Read-only: the state of the Stripe mirror, shown on the edit page --------------------

    public string? ProviderProductId { get; set; }
    public string? ProviderPriceId { get; set; }
    public DateTimeOffset? ProviderSyncedAt { get; set; }
    public string? ProviderSyncError { get; set; }
    public bool IsProviderSynced => ProviderPriceId is not null && ProviderSyncedAt is not null && ProviderSyncError is null;

    /// <summary>Decides whether the delete button is offered, and what it says.</summary>
    public PlanUsage? Usage { get; set; }

    /// <summary>Whether the configured key is a live one — decides which Stripe dashboard the
    /// product links point at.</summary>
    public bool StripeLive { get; set; }

    public string? StripeProductUrl => ProviderProductId is null ? null
        : $"https://dashboard.stripe.com/{(StripeLive ? "" : "test/")}products/{ProviderProductId}";

    public MembershipPlan ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Name = Name.Trim(),
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
        PriceMinor = (long)Math.Round(Price * 100, MidpointRounding.AwayFromZero),
        Currency = Currency.Trim().ToUpperInvariant(),
        BillingPeriod = BillingPeriod,
        Features = Features,
        Status = Status,
        SortOrder = SortOrder,
        MaxMembers = MaxMembers,
    };

    public static AdminMembershipPlanFormViewModel FromEntity(MembershipPlan p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Price = p.PriceMinor / 100m,
        Currency = p.Currency,
        BillingPeriod = p.BillingPeriod,
        Features = p.Features,
        Status = p.Status,
        SortOrder = p.SortOrder,
        MaxMembers = p.MaxMembers,
        ProviderProductId = p.ProviderProductId,
        ProviderPriceId = p.ProviderPriceId,
        ProviderSyncedAt = p.ProviderSyncedAt,
        ProviderSyncError = p.ProviderSyncError,
    };
}
