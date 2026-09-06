using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

public class ExperienceFaq : BaseEntity
{
    public Guid ExperienceId { get; set; }

    /// <summary>
    /// Which language this question and answer are written in. See ExperienceInclusion.Culture.
    /// </summary>
    public string Culture { get; set; } = "en-GB";
    public string Question { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public int SortOrder { get; set; }
}
