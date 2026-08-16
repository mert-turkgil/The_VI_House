using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

public class ExperienceProgramDay : BaseEntity
{
    public Guid ExperienceId { get; set; }
    public int DayNumber { get; set; }
    public string? DateLabel { get; set; }
    public string Title { get; set; } = default!;
    public int SortOrder { get; set; }

    public List<ExperienceSession> Sessions { get; set; } = [];
}
