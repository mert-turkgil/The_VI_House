using VIHouse.Entities.Common;

namespace VIHouse.Entities.Journal;

/// <summary>
/// One culture's worth of a journal post's copy. The same shape as SeminarTranslation, and
/// deliberately so: a reader who switches language should get the same fallback behaviour whichever
/// kind of content they are looking at.
///
/// <see cref="Body"/> is authored in the admin panel's rich text editor and rendered with Html.Raw,
/// so it is always written through JournalService, which sanitises it (see EditorHtml). Never
/// assign to it from raw request input.
/// </summary>
public class JournalPostTranslation : BaseEntity
{
    public Guid JournalPostId { get; set; }

    /// <summary>Culture name exactly as RequestLocalizationOptions supplies it, e.g. "en-GB".</summary>
    public string Culture { get; set; } = default!;

    public string Title { get; set; } = default!;

    /// <summary>The listing card's standfirst, and the search result's subtitle.</summary>
    public string? Excerpt { get; set; }

    /// <summary>Sanitised HTML. Posts written before the rich text editor existed are stored as
    /// plain text with blank-line-separated paragraphs and are converted on read.</summary>
    public string Body { get; set; } = default!;
}
