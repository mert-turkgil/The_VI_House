using VIHouse.Entities.Experiences;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminExperienceListItemViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public DateTimeOffset StartAtUtc { get; set; }
    public ExperienceStatus Status { get; set; }
    public int TicketTypeCount { get; set; }
}
