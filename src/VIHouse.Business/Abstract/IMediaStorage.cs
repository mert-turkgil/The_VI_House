using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Abstract;

/// <summary>
/// Where uploaded seminar assets are put. Provider-agnostic for the same reason
/// <see cref="IPaymentProvider"/> is: the local-disk implementation (WebUI/Services/LocalMediaStorage)
/// is what Phase 1 ships, and moving to blob storage later means writing one class against this
/// contract rather than touching SeminarService.
///
/// Nothing here deals in URLs. Assets are addressed by an opaque <em>storage key</em> and are never
/// mapped into the static-file pipeline, because a seminar can be priced and members-only: a file
/// sitting under a guessable — or merely shareable — public path would hand the recording to
/// anyone the link reached. Everything is streamed instead, through SeminarsController, which
/// checks the enrolment first. See <see cref="GetAsync"/>.
///
/// Implementations must treat every upload as hostile. The admin panel is the only caller today,
/// but "a file the server writes under a name the client chose" is the most reliable way to turn a
/// content editor into remote code execution — see <see cref="MediaPolicy"/> for the required rules.
/// </summary>
public interface IMediaStorage
{
    /// <summary>
    /// Validates and stores one upload, returning the key to address it by. Never throws for a
    /// rejected file: an unsupported type or an oversized video is an ordinary thing for an admin to
    /// do by accident, and belongs on the form as a message rather than in the logs as a 500.
    /// </summary>
    Task<MediaSaveResult> SaveAsync(MediaUpload upload, string folder, CancellationToken ct = default);

    /// <summary>
    /// Resolves a stored asset for streaming, or null if the key does not name a file we hold. The
    /// content type comes back from storage rather than from the request, so a response can never be
    /// labelled as something other than what was validated on the way in.
    /// </summary>
    Task<MediaFileInfo?> GetAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Removes a stored asset. A key that points outside the media root, or at a file that is
    /// already gone, is ignored rather than treated as an error — deleting a seminar whose file was
    /// cleaned up by hand must still work.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}

/// <summary>One file on its way in. <see cref="Content"/> is owned by the caller, not the storage.</summary>
public record MediaUpload(string FileName, string ContentType, long Length, Stream Content);

/// <param name="PhysicalPath">Absolute path, for a range-enabled PhysicalFileResult.</param>
public record MediaFileInfo(string PhysicalPath, string ContentType, long SizeBytes);

public record MediaSaveResult(bool Success, string? StorageKey, string? ContentType, long SizeBytes, string? Error)
{
    public static MediaSaveResult Ok(string storageKey, string contentType, long sizeBytes) =>
        new(true, storageKey, contentType, sizeBytes, null);

    /// <summary>The error is a SharedResource key, not a sentence — see SeminarSaveResult.</summary>
    public static MediaSaveResult Fail(string error) => new(false, null, null, 0, error);
}

/// <summary>
/// The single source of truth for what may be uploaded, how big it may be, and what it renders as.
///
/// Kept here rather than inside the storage implementation because two places need to agree on it:
/// the implementation, which enforces it, and the admin UI, which has to tell an admin what they
/// are allowed to pick before they spend ten minutes uploading a file that will be rejected.
///
/// The allow-list is by extension, and the stored content type is the one this table maps that
/// extension to — never the one the browser sent, which is a claim rather than a fact.
/// </summary>
public static class MediaPolicy
{
    /// <summary>Extension (lowercase, with dot) to the content type it will be stored and served as.</summary>
    public static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".gif"] = "image/gif",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mov"] = "video/quicktime",
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".pdf"] = "application/pdf",

        // SVG is deliberately absent. It is an image everywhere in a UI and a script host
        // everywhere in a browser, and there is no way to serve an author-supplied one from our own
        // origin without handing it our session cookies.
    };

    /// <summary>Per-kind ceiling in bytes. A recording is allowed to be genuinely large; a cover
    /// image that size is a mistake, and letting it through only makes pages slow.</summary>
    public static long MaxBytesFor(SeminarMediaKind kind) => kind switch
    {
        SeminarMediaKind.Video => 512L * 1024 * 1024,
        SeminarMediaKind.Audio => 128L * 1024 * 1024,
        SeminarMediaKind.Animation => 24L * 1024 * 1024,
        SeminarMediaKind.Document => 48L * 1024 * 1024,
        _ => 12L * 1024 * 1024,
    };

    /// <summary>The largest any single upload may be — the request-level limit, matching the most
    /// permissive per-kind ceiling above.</summary>
    public const long MaxUploadBytes = 512L * 1024 * 1024;

    /// <summary>
    /// What an asset renders as, derived from the extension rather than chosen by the admin, so the
    /// stored kind can never disagree with the bytes on disk. Returns null for anything not on the
    /// allow-list.
    /// </summary>
    public static SeminarMediaKind? Classify(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !AllowedTypes.ContainsKey(extension))
            return null;

        return extension.ToLowerInvariant() switch
        {
            ".gif" => SeminarMediaKind.Animation,
            ".mp4" or ".webm" or ".mov" => SeminarMediaKind.Video,
            ".mp3" or ".m4a" => SeminarMediaKind.Audio,
            ".pdf" => SeminarMediaKind.Document,
            _ => SeminarMediaKind.Image,
        };
    }

    /// <summary>Accept list for the file input, so the picker filters before the upload starts.</summary>
    public static string AcceptAttribute => string.Join(",", AllowedTypes.Keys);
}
