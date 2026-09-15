using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>Posted by the "Grant membership" form on the user record — see AdminUsersController.GrantMembership.</summary>
public class AdminGrantMembershipViewModel
{
    [Required]
    public Guid PlanId { get; set; }

    /// <summary>Blank = the plan's own period from today (never, for a one-time plan).</summary>
    public DateOnly? ExpiresOn { get; set; }

    /// <summary>Grant a seat even when the plan is at its member limit.</summary>
    public bool OverrideCap { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>Posted by the "Make ambassador" form on the user record — see AdminUsersController.MakeAmbassador.</summary>
public class AdminMakeAmbassadorViewModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [Required, StringLength(40)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Letters, digits and hyphens only.")]
    public string Code { get; set; } = "";

    [Range(0, 100)]
    public decimal CommissionPercent { get; set; } = 10;
}
