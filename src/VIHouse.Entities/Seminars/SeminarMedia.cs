using VIHouse.Entities.Common;

namespace VIHouse.Entities.Seminars;

/// <summary>
/// An uploaded asset attached to a seminar — the recording, a clip, an animated explainer, the
/// slide deck. Stored as a row rather than only as markup inside
/// <see cref="SeminarTranslation.BodyHtml"/> so that the same asset can be listed, reordered and
/// deleted without editing prose, so a video is served by a real player element rather than
/// whatever tag survived a paste, and — the part that matters — so every byte is addressable
/// through something that can check an enrolment first.
/// </summary>
public class SeminarMedia : BaseEntity
{
    public Guid SeminarId { get; set; }

    /// <summary>
    /// Opaque key into <see cref="Business.Abstract.IMediaStorage"/> — deliberately not a URL.
    /// Nothing serves this path directly; the browser only ever sees the route
    /// /sessions/{slug}/media/{id}, which resolves this after checking the viewer's access.
    /// </summary>
    public string StorageKey { get; set; } = default!;

    public SeminarMediaKind Kind { get; set; }

    /// <summary>Caption under the asset, and the alt text for a still image.</summary>
    public string? Title { get; set; }

    /// <summary>
    /// True for an asset the admin dropped into the rich text editor, which inserts its own
    /// &lt;img&gt; into the body. Those still belong in the library — they need the same access
    /// check, and deleting the seminar has to clean them up — but repeating them underneath the
    /// article as a gallery would show every inline picture twice.
    /// </summary>
    public bool IsInline { get; set; }

    /// <summary>As stored, for the response's Content-Type and for the admin's media list. Always
    /// the type MediaPolicy maps the extension to, never the one the browser claimed.</summary>
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }

    /// <summary>The name the admin uploaded it under — the only way to recognise a file once its
    /// stored name is a GUID.</summary>
    public string? OriginalFileName { get; set; }

    public int SortOrder { get; set; }
}
