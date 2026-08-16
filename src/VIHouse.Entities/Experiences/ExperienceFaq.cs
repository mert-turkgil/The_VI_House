using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

public class ExperienceFaq : BaseEntity
{
    public Guid ExperienceId { get; set; }
    public string Question { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public int SortOrder { get; set; }
}
