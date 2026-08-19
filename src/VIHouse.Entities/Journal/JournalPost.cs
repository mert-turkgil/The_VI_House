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

    /// <summary>Plain text; blank-line-separated paragraphs, split for rendering in the Razor view —
    /// same technique already used for Legal page body text (Views/Legal/Show.cshtml).</summary>
    public string Body { get; set; } = default!;

    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }

    /// <summary>Set once, the first time Status flips to Published. Drives "newest first"
    /// sort/display; a later unpublish/republish cycle does not reset it.</summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
