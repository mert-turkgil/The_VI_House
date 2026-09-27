using VIHouse.Entities.Applications;

namespace VIHouse.WebUI.ViewModels.Applications;

public class ApplicationStatusViewModel
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = default!;
    public ApplicationStatus Status { get; set; }
    public string ExperienceLabel { get; set; } = default!;
    public DateTimeOffset? SubmittedAt { get; set; }
}

/// <summary>The status page: every application this visitor is allowed to see.</summary>
public class ApplicationStatusListViewModel
{
    public List<ApplicationStatusViewModel> Items { get; set; } = [];
    public bool SignedIn { get; set; }
}
