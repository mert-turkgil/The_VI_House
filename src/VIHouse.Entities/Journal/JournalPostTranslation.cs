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

    // --- Search ---------------------------------------------------------------------------------
    // Added to match SeminarTranslation and ExperienceTranslation, which have carried these all
    // along. Without them a translated post could only ever present its headline to a search engine,
    // and a headline written to work above an article is rarely the one that works as a blue link.
    // Both are optional: empty falls back to Title and Excerpt, which is what happened before.

    /// <summary>Overrides the title in search results and browser tabs. Aim for under 60 characters.</summary>
    public string? SeoTitle { get; set; }

    /// <summary>Overrides the excerpt as the meta description. Google shows roughly 160 characters.</summary>
    public string? SeoDescription { get; set; }
}
