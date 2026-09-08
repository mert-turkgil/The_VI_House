using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AddFaqInputModel
{
    public Guid ExperienceId { get; set; }

    /// <summary>Which language tab this row was added from — see ExperienceInclusion.Culture.</summary>
    public string Culture { get; set; } = "en-GB";

    [Required, StringLength(300)]
    public string Question { get; set; } = default!;

    [Required]
    public string Answer { get; set; } = default!;
}
