using VIHouse.Entities.Applications;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminApplicationDetailViewModel
{
    public Application Application { get; set; } = default!;
    public string ExperienceLabel { get; set; } = default!;
    public string? ReviewedByEmail { get; set; }

    public bool CanMarkUnderReview => Application.Status == ApplicationStatus.Submitted;
    public bool CanShortlist => Application.Status == ApplicationStatus.UnderReview;
    public bool CanDecide => Application.Status == ApplicationStatus.Shortlisted;
}
