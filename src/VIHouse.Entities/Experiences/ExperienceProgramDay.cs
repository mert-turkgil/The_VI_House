using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

public class ExperienceProgramDay : BaseEntity
{
    public Guid ExperienceId { get; set; }

    /// <summary>
    /// Which language this day and its sessions are written in. See ExperienceInclusion.Culture.
    /// </summary>
    public string Culture { get; set; } = "en-GB";
    public int DayNumber { get; set; }
    public string? DateLabel { get; set; }
    public string Title { get; set; } = default!;
    public int SortOrder { get; set; }

    public List<ExperienceSession> Sessions { get; set; } = [];
}
