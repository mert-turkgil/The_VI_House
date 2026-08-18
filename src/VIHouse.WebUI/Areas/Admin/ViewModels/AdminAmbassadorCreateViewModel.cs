using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminAmbassadorCreateViewModel
{
    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = default!;

    [Required, StringLength(150)]
    public string Name { get; set; } = default!;

    [Required, StringLength(40)]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "Letters, numbers and hyphens only — this becomes part of a public URL.")]
    public string Code { get; set; } = default!;

    [Required, Range(0, 100)]
    public decimal CommissionPercent { get; set; }
}
