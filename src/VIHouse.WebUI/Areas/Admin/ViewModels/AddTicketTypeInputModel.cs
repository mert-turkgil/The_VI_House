using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AddTicketTypeInputModel
{
    public Guid ExperienceId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = default!;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required, Range(0, 10_000_000)]
    [Display(Name = "Price (minor units, e.g. pence)")]
    public long PriceMinor { get; set; }

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "GBP";

    [Required, Range(0, 100_000)]
    public int Inventory { get; set; }

    [Range(1, 100)]
    [Display(Name = "Max Qty Per Order")]
    public int MaxQuantityPerOrder { get; set; } = 1;

    [StringLength(500)]
    [Display(Name = "Perks")]
    public string? PerksText { get; set; }
}
