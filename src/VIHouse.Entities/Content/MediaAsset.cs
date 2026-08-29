using VIHouse.Entities.Common;

namespace VIHouse.Entities.Content;

/// <summary>
/// A file uploaded through the admin panel that belongs to no single record — the photograph behind
/// an ecosystem card, a partner's logo, a testimonial's portrait.
///
/// Everything else that holds a file hangs it off its owner (HeroSlide.ImageStorageKey,
/// JournalPostMedia). Content blocks cannot: a block stores its rows as JSON, and a JSON row is not
/// something a foreign key can point at. Without somewhere to put them, those images had to be paths
/// typed by hand into a text box, which meant every one of them had to be committed to wwwroot
/// first — and an upload into wwwroot is exactly the thing that works in Development and 404s in
/// Production, because MapStaticAssets only serves files that existed at build time.
///
/// So these live in the media root like every other upload and are streamed by MediaController.
/// Being unowned, they are deleted deliberately from the assets panel rather than automatically:
/// nothing can prove a URL sitting inside a JSON blob is unused.
/// </summary>
public class MediaAsset : BaseEntity
{
    /// <summary>Opaque key into IMediaStorage. Never a URL, never built from request input.</summary>
    public string StorageKey { get; set; } = default!;

    /// <summary>Admin-facing label. Falls back to the original filename in the panel.</summary>
    public string? Title { get; set; }

    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string? OriginalFileName { get; set; }
}
