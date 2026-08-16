using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AddInclusionInputModel
{
    public Guid ExperienceId { get; set; }

    [Required, StringLength(500)]
    public string Text { get; set; } = default!;

    [Display(Name = "Included (unchecked = shown under Not Included)")]
    public bool IsIncluded { get; set; } = true;
}
