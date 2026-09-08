using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AddInclusionInputModel
{
    public Guid ExperienceId { get; set; }

    /// <summary>Which language tab this row was added from — see ExperienceInclusion.Culture.</summary>
    public string Culture { get; set; } = "en-GB";

    [Required, StringLength(500)]
    public string Text { get; set; } = default!;

    [Display(Name = "Included (unchecked = shown under Not Included)")]
    public bool IsIncluded { get; set; } = true;
}
