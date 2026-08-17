using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.ViewModels.Applications;

public class SubmittedViewModel
{
    public Experience Experience { get; set; } = default!;
    public Guid ApplicationId { get; set; }
}
