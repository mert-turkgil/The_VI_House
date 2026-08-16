using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

/// <summary>A single "What's included" / "Not included" line item (brief §22).</summary>
public class ExperienceInclusion : BaseEntity
{
    public Guid ExperienceId { get; set; }
    public string Text { get; set; } = default!;
    public bool IsIncluded { get; set; }
    public int SortOrder { get; set; }
}
