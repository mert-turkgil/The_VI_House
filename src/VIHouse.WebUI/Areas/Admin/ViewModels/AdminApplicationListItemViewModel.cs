using VIHouse.Entities.Applications;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminApplicationListItemViewModel
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string ExperienceLabel { get; set; } = default!;
    public ApplicationStatus Status { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}
