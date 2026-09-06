using VIHouse.Entities.Common;

namespace VIHouse.Entities.Experiences;

/// <summary>
/// One photograph in an experience's gallery. The caption doubles as the alt text — see the note on
/// <see cref="AltText"/>.
/// </summary>
public class ExperienceImage : BaseEntity
{
    public Guid ExperienceId { get; set; }

    /// <summary>A site-relative path, for a file committed under wwwroot. Null once uploaded.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// Set when the image was uploaded rather than typed, exactly as
    /// <see cref="Experience.CoverImageStorageKey"/> works: an upload wins and nulls the URL.
    /// </summary>
    public string? StorageKey { get; set; }

    /// <summary>
    /// Rendered both as the img alt attribute and as the visible caption under the photograph, which
    /// is why it reads as a sentence ("A long table set for a private dinner") rather than a label.
    /// </summary>
    public string? AltText { get; set; }

    public int SortOrder { get; set; }
}
