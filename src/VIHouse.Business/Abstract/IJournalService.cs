using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Journal;

namespace VIHouse.Business.Abstract;

public interface IJournalService
{
    // --- Public ---
    Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default);
    Task<JournalPost?> GetPublicDetailBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<JournalPost>> SearchPublishedAsync(string term, CancellationToken ct = default);

    /// <summary>Resolves one media row for the public streaming endpoint, or null if it is gone.</summary>
    Task<JournalPostMedia?> GetMediaAsync(Guid mediaId, CancellationToken ct = default);

    /// <summary>Opens the bytes behind a media row. Null when the row or the file is missing.</summary>
    Task<MediaFileInfo?> OpenMediaAsync(Guid mediaId, CancellationToken ct = default);

    // --- Admin --- (every mutation is audit-logged)
    Task<List<JournalPost>> GetAllForAdminAsync(CancellationToken ct = default);
    Task<JournalPost?> GetForAdminEditAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates a post and its default-culture copy together — a post with no words in any
    /// language is not something the site can render.</summary>
    Task<JournalSaveResult> CreateAsync(
        JournalPost post, JournalPostTranslation defaultTranslation, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Saves the language-independent fields. Never touches translations or media.</summary>
    Task<JournalSaveResult> UpdateAsync(JournalPost updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Writes one language's copy, creating the row if this is the first time. Sanitises
    /// the body and prunes any inline file no body references any more.</summary>
    Task<JournalSaveResult> SaveTranslationAsync(
        Guid postId, JournalPostTranslation translation, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<JournalSaveResult> DeleteTranslationAsync(
        Guid postId, string culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Permanently removes a post, its translations and every file it owns. Returns a
    /// failure result when the id no longer exists (e.g. a double-submitted delete), so the caller
    /// can report that without treating it as an error.</summary>
    Task<JournalSaveResult> DeleteAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- Admin: media ---

    /// <summary>Stores one upload against a post. <paramref name="isInline"/> marks an asset the
    /// editor put into the body, which is what makes it eligible for pruning later.</summary>
    Task<JournalMediaResult> AddMediaAsync(
        Guid postId, MediaUpload upload, string? title, bool isInline,
        Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Removes one asset — row first, then the file.</summary>
    Task<JournalSaveResult> RemoveMediaAsync(Guid postId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Points the post's cover at an existing image, replacing whatever it was. The file
    /// the previous cover used is deleted only if nothing else references it.</summary>
    Task<JournalSaveResult> SetCoverAsync(Guid postId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Uploads a new cover in one step, deleting the file the old one used.</summary>
    Task<JournalSaveResult> ReplaceCoverAsync(
        Guid postId, MediaUpload upload, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<JournalSaveResult> RemoveCoverAsync(Guid postId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}

/// <summary>
/// Outcome of an admin write. <see cref="Error"/> is a SharedResource key rather than a sentence,
/// so the admin panel says it in whichever language the admin is reading — the same contract
/// SeminarSaveResult uses.
/// </summary>
public record JournalSaveResult(bool Success, Guid? PostId, string? Error)
{
    public static JournalSaveResult Ok(Guid? postId = null) => new(true, postId, null);
    public static JournalSaveResult Fail(string error) => new(false, null, error);
}

public record JournalMediaResult(bool Success, JournalPostMedia? Media, string? Error)
{
    public static JournalMediaResult Ok(JournalPostMedia media) => new(true, media, null);
    public static JournalMediaResult Fail(string error) => new(false, null, error);
}
