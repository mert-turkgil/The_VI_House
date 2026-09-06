using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

/// <summary>A single "What's included" / "Not included" line item (brief §22).</summary>
public class ExperienceInclusion : BaseEntity
{
    public Guid ExperienceId { get; set; }

    /// <summary>
    /// Which language this line is written in. These lists are not translations of each other row for
    /// row — what is included reads as a different number of lines in different languages — so a row
    /// belongs to a culture and the public page shows the set for the reader's, falling back to the
    /// default when a language has none.
    /// </summary>
    public string Culture { get; set; } = "en-GB";
    public string Text { get; set; } = default!;
    public bool IsIncluded { get; set; }
    public int SortOrder { get; set; }
}
