using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

public class ExperienceImage : BaseEntity
{
    public Guid ExperienceId { get; set; }
    public string Url { get; set; } = default!;
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
}
