using VIHouse.Entities.Common;

namespace VIHouse.Entities.Journal;

/// <summary>
/// Brief §125: SEO/thought-leadership editorial content — Founder Stories, Business, Technology,
/// Capital, Culture, House Notes. Admin-authored only, never user-generated; deliberately not
/// branded "Blog" anywhere in the UI (brief §126).
/// </summary>
public class JournalPost : BaseEntity
{
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public JournalCategory Category { get; set; }
    public JournalPostStatus Status { get; set; } = JournalPostStatus.Draft;

    public string? Excerpt { get; set; }

    /// <summary>Sanitised HTML, authored in the admin panel's rich text editor and rendered with
    /// Html.Raw. Always written through JournalService, which sanitises it (see EditorHtml) — never
    /// assign to this from raw request input. Posts predating the editor are stored as plain text
    /// with blank-line-separated paragraphs and are converted on read.</summary>
    public string Body { get; set; } = default!;

    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }

    /// <summary>Set once, the first time Status flips to Published. Drives "newest first"
    /// sort/display; a later unpublish/republish cycle does not reset it.</summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
