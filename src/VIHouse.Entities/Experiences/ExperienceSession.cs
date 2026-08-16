using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

public class ExperienceSession : BaseEntity
{
    public Guid ProgramDayId { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? SpeakerName { get; set; }
    public int SortOrder { get; set; }
}
