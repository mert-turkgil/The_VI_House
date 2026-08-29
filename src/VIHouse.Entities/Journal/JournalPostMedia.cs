using VIHouse.Entities.Common;
using VIHouse.Entities.Seminars;

namespace VIHouse.Entities.Journal;

/// <summary>
/// A file belonging to one journal post: the images and GIFs dropped into the article, the audio an
/// author attaches, and the uploaded cover.
///
/// Every file the site holds is owned by a row like this one — that ownership is what makes the
/// lifecycle enforceable. Replacing an asset deletes the file it replaced, deleting the post takes
/// every file with it, and an inline image removed from the body is pruned on the next save. Files
/// that belong to nothing cannot be cleaned up by anything, which is how a media directory fills
/// with debris nobody dares delete.
///
/// <see cref="Kind"/> reuses <see cref="SeminarMediaKind"/> rather than declaring a parallel enum.
/// The name says "Seminar" for historical reasons only: it describes what an asset renders as, and
/// that question has exactly one answer per file type across the whole site. MediaPolicy classifies
/// into it from the extension, so the same allow-list governs both.
/// </summary>
public class JournalPostMedia : BaseEntity
{
    public Guid JournalPostId { get; set; }

    /// <summary>Opaque key into IMediaStorage — never a URL, and never built from request input.</summary>
    public string StorageKey { get; set; } = default!;

    public SeminarMediaKind Kind { get; set; }

    /// <summary>Admin-facing label in the media library. Never shown to a reader.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// True for a file the editor uploaded into the body itself. Inline assets are hidden from the
    /// media library's "attachments" list (the article already shows them) and are the ones the
    /// prune step reclaims once no translation's body references them any more.
    /// </summary>
    public bool IsInline { get; set; }

    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string? OriginalFileName { get; set; }
    public int SortOrder { get; set; }
}
