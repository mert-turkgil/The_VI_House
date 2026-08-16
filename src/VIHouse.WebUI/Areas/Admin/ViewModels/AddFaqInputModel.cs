using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AddFaqInputModel
{
    public Guid ExperienceId { get; set; }

    [Required, StringLength(300)]
    public string Question { get; set; } = default!;

    [Required]
    public string Answer { get; set; } = default!;
}
