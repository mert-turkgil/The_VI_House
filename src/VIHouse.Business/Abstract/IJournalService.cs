using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Journal;

namespace VIHouse.Business.Abstract;

public interface IJournalService
{
    // --- Public ---
    Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default);
    Task<JournalPost?> GetPublicDetailBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<JournalPost>> SearchPublishedAsync(string term, CancellationToken ct = default);

    /// <summary>Opens the bytes behind a media row, with what the caller needs to decide who may
    /// see them. Null when the row, its post or the file is missing.</summary>
    Task<JournalMediaFile?> OpenMediaAsync(Guid mediaId, CancellationToken ct = default);

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

    /// <summary>
    /// Saves the media library as the admin arranged it: <paramref name="items"/> is in the order
    /// the gallery should show, and carries each file's captions and whether it is shown at all.
    /// Files not listed keep their captions and follow the listed ones in their existing order.
    /// </summary>
    Task<JournalSaveResult> SaveGalleryAsync(
        Guid postId, IReadOnlyList<JournalGalleryEdit> items, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Points the post's cover at an existing image, replacing whatever it was. The file
    /// the previous cover used is deleted only if nothing else references it.</summary>
    Task<JournalSaveResult> SetCoverAsync(Guid postId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Uploads a new cover in one step, deleting the file the old one used.</summary>
    Task<JournalSaveResult> ReplaceCoverAsync(
        Guid postId, MediaUpload upload, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<JournalSaveResult> RemoveCoverAsync(Guid postId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- Admin: influencer submissions ---

    /// <summary>Sends a submitted article back to its author with a note they see. The author is
    /// told by bell and email.</summary>
    Task<JournalSaveResult> RequestChangesAsync(Guid postId, string note, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Articles waiting for the editors — the sidebar badge and the Submissions filter.</summary>
    Task<int> CountSubmittedAsync(CancellationToken ct = default);

    // --- Influencer authors --- (English only; the editors translate and publish)

    /// <summary>One author's own posts, newest first.</summary>
    Task<List<JournalPost>> GetForAuthorAsync(Guid authorUserId, CancellationToken ct = default);

    /// <summary>The post with its copy and files, or null when it is not this author's.</summary>
    Task<JournalPost?> GetOwnAsync(Guid postId, Guid authorUserId, CancellationToken ct = default);

    /// <summary>As <see cref="GetOwnAsync"/>, and only while the author may change it (Draft or
    /// sent back). The guard in front of every author upload and save.</summary>
    Task<JournalPost?> GetEditableForAuthorAsync(Guid postId, Guid authorUserId, CancellationToken ct = default);

    /// <summary>A new draft credited to the author, named after their display name.</summary>
    Task<JournalSaveResult> StartForAuthorAsync(Guid authorUserId, string authorName, JournalCategory category,
        string title, string? excerpt, string? ipAddress, CancellationToken ct = default);

    /// <summary>Saves the author's English copy, category and cover description; with
    /// <paramref name="submit"/>, also hands it to the editors, who are told.</summary>
    Task<JournalSaveResult> SaveForAuthorAsync(Guid postId, Guid authorUserId, JournalAuthorDraft draft, bool submit,
        string? ipAddress, CancellationToken ct = default);

    /// <summary>The author takes a submission back to keep working on it.</summary>
    Task<JournalSaveResult> WithdrawSubmissionAsync(Guid postId, Guid authorUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Whether the author has at least one published article — what makes their photo public.</summary>
    Task<bool> HasPublishedAsync(Guid authorUserId, CancellationToken ct = default);
}

/// <summary>What an influencer edits on their article: the English copy, the category and the
/// cover's description.</summary>
public record JournalAuthorDraft(string Title, string? Excerpt, string Body, JournalCategory Category, string? CoverImageAlt);

/// <summary>A journal file and who may see it: everyone once its post is published, otherwise the
/// staff and the post's author.</summary>
public record JournalMediaFile(MediaFileInfo File, bool IsPublic, Guid? AuthorUserId);

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

/// <summary>One library file as the admin left it: captions by culture (blank = none in that
/// language) and whether it appears in the gallery.</summary>
public record JournalGalleryEdit(Guid MediaId, IReadOnlyDictionary<string, string?> Captions, bool ShowInGallery);

/// <param name="Reused">True when the upload was identical to a file the post already had, so that
/// file was returned and nothing new was stored.</param>
public record JournalMediaResult(bool Success, JournalPostMedia? Media, string? Error, bool Reused = false)
{
    public static JournalMediaResult Ok(JournalPostMedia media, bool reused = false) => new(true, media, null, reused);
    public static JournalMediaResult Fail(string error) => new(false, null, error);
}
