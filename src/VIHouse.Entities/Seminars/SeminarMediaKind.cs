namespace VIHouse.Entities.Seminars;

/// <summary>
/// What to render an asset as. Derived from the upload's content type, not chosen by hand — see
/// IMediaStorage — so it can never disagree with the bytes on disk.
/// </summary>
public enum SeminarMediaKind
{
    /// <summary>Still image: JPEG, PNG, WebP, SVG.</summary>
    Image,

    /// <summary>GIF or animated WebP. Separate from Image because it autoplays and loops, and
    /// because it is the one "video" that is really a picture.</summary>
    Animation,

    /// <summary>MP4/WebM, rendered in a &lt;video&gt; element with controls.</summary>
    Video,

    Audio,

    /// <summary>Slide deck or handout — offered as a download, never inlined.</summary>
    Document,
}
