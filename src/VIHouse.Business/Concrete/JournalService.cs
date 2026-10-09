using System.Security.Cryptography;
using System.Text.Json;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Journal posts: their copy in each language, and the files that belong to them.
///
/// Two rules run through everything below and are worth stating once.
///
/// **Files are owned.** Every byte on disk is named by a JournalPostMedia row. Replacing an asset
/// deletes the file it replaced, deleting a post deletes all of them, and an inline image dropped
/// from the body is reclaimed on the next save (see <see cref="PruneInlineMediaAsync"/>). Nothing
/// here writes a file that nothing points at.
///
/// **Database first, files second.** Every delete commits the row change before touching the disk.
/// The failure that ordering protects against is the one that cannot be undone: a constraint we did
/// not anticipate leaves a post whole with its media intact, rather than live with its assets
/// already erased. The reverse order has no recovery.
/// </summary>
public class JournalService(
    IJournalPostRepository posts,
    // Generic repositories for the child collections, used for inserts and deletes rather than
    // pushing into the loaded post's navigation properties — same reasoning as SeminarService: EF
    // cannot reliably tell a new entity from an existing one by graph discovery once it carries a
    // client-generated Guid key, and states it as Modified instead of Added.
    IRepository<JournalPostTranslation> translations,
    IRepository<JournalPostMedia> mediaRows,
    IMediaStorage mediaStorage,
    IAuditLogRepository auditLogs) : IJournalService
{
    /// <summary>
    /// How an asset is addressed once it is in an article body. Slug-free by design: this URL is
    /// written into the HTML, where it has to survive the post being renamed.
    /// </summary>
    public static string MediaUrl(Guid mediaId) => $"/media/journal/{mediaId}";

    // --- Public reads ----------------------------------------------------------------------------

    public Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default) =>
        posts.GetPublicListingAsync(filter, ct);

    public Task<JournalPost?> GetPublicDetailBySlugAsync(string slug, CancellationToken ct = default) =>
        posts.GetBySlugAsync(slug, ct);

    public Task<List<JournalPost>> SearchPublishedAsync(string term, CancellationToken ct = default) =>
        posts.SearchPublishedAsync(term, ct);

    public Task<JournalPostMedia?> GetMediaAsync(Guid mediaId, CancellationToken ct = default) =>
        posts.GetMediaAsync(mediaId, ct);

    public async Task<MediaFileInfo?> OpenMediaAsync(Guid mediaId, CancellationToken ct = default)
    {
        var media = await posts.GetMediaAsync(mediaId, ct);
        // The storage key comes from the row, never from the request — which is what keeps this
        // from being an arbitrary-file reader with a Guid for a filename.
        return media is null ? null : await mediaStorage.GetAsync(media.StorageKey, ct);
    }

    // --- Admin: the post itself --------------------------------------------------------------------

    public Task<List<JournalPost>> GetAllForAdminAsync(CancellationToken ct = default) =>
        posts.GetAllWithTranslationsAsync(ct);

    public Task<JournalPost?> GetForAdminEditAsync(Guid id, CancellationToken ct = default) =>
        posts.GetWithDetailAsync(id, ct);

    public async Task<JournalSaveResult> CreateAsync(
        JournalPost post, JournalPostTranslation defaultTranslation, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        // The slug is optional at creation: an empty one comes from the title. A generated slug that
        // is already taken gets -2, -3 … (nobody chose it, so nobody should have to fix it); a slug
        // the editor typed that is taken is refused with a message.
        var typed = Slugs.From(post.Slug);
        var slug = typed.Length > 0 ? typed : Slugs.From(defaultTranslation.Title);
        if (slug.Length == 0) return JournalSaveResult.Fail("Journal.Error.SlugEmpty");
        if (typed.Length > 0)
        {
            if (await posts.SlugExistsAsync(slug, null, ct)) return JournalSaveResult.Fail("Journal.Error.SlugTaken");
        }
        else
        {
            var candidate = slug;
            for (var n = 2; await posts.SlugExistsAsync(candidate, null, ct); n++) candidate = $"{slug}-{n}";
            slug = candidate;
        }
        post.Slug = slug;

        defaultTranslation.JournalPostId = post.Id;
        defaultTranslation.Culture = SiteCultures.Default;
        defaultTranslation.Body = EditorHtml.Sanitize(defaultTranslation.Body);
        post.Translations.Add(defaultTranslation);

        if (post.Status == JournalPostStatus.Published)
        {
            if (string.IsNullOrWhiteSpace(defaultTranslation.Body))
                return JournalSaveResult.Fail("Journal.Error.BodyRequiredToPublish");

            post.PublishedAt = DateTimeOffset.UtcNow;
        }

        await posts.AddAsync(post, ct);
        await LogAsync("JournalPostCreated", post.Id, adminUserId, ipAddress,
            before: null, after: new { defaultTranslation.Title, post.Slug, post.Category, post.Status }, ct);
        await posts.SaveChangesAsync(ct);

        return JournalSaveResult.Ok(post.Id);
    }

    public async Task<JournalSaveResult> UpdateAsync(
        JournalPost updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await posts.GetWithDetailAsync(updated.Id, ct);
        if (existing is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        // Publishing is gated on the default culture having a body, the same precondition
        // SeminarService.SetStatusAsync enforces: every other language falls back to it, so a post
        // published without one is four empty pages rather than one.
        if (updated.Status == JournalPostStatus.Published
            && string.IsNullOrWhiteSpace(JournalContent.Find(existing, SiteCultures.Default)?.Body))
        {
            return JournalSaveResult.Fail("Journal.Error.BodyRequiredToPublish");
        }

        var slug = Slugs.From(updated.Slug);
        if (slug.Length == 0) return JournalSaveResult.Fail("Journal.Error.SlugEmpty");
        if (await posts.SlugExistsAsync(slug, existing.Id, ct)) return JournalSaveResult.Fail("Journal.Error.SlugTaken");

        var before = new { existing.Slug, existing.Category, existing.Status };

        existing.Slug = slug;
        existing.Category = updated.Category;
        existing.CoverImageUrl = updated.CoverImageUrl;
        existing.CoverImageAlt = updated.CoverImageAlt;
        existing.AuthorName = updated.AuthorName;
        existing.Status = updated.Status;
        if (existing.PublishedAt is null && updated.Status == JournalPostStatus.Published)
            existing.PublishedAt = DateTimeOffset.UtcNow;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("JournalPostUpdated", existing.Id, adminUserId, ipAddress,
            before, new { existing.Slug, existing.Category, existing.Status }, ct);

        // No explicit Update() call: `existing` is already tracked, loaded on this same scoped
        // DbContext — same reasoning as ExperienceService.UpdateCoreFieldsAsync.
        await posts.SaveChangesAsync(ct);

        return JournalSaveResult.Ok(existing.Id);
    }

    public async Task<JournalSaveResult> DeleteAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await posts.GetWithDetailAsync(id, ct);
        if (existing is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        // Captured before the delete, since the navigation is cleared as part of it.
        var storageKeys = existing.Media.Select(m => m.StorageKey).ToList();
        var title = JournalContent.Title(existing, SiteCultures.Default);

        // Unlike an Experience, a journal post has no dependent records (no bookings, applications
        // or payments reference it), so a delete is always safe. The audit entry keeps the
        // title/slug recoverable afterwards.
        posts.Remove(existing);
        await LogAsync("JournalPostDeleted", existing.Id, adminUserId, ipAddress,
            before: new { Title = title, existing.Slug, existing.Category, existing.Status, MediaCount = storageKeys.Count },
            after: null, ct);
        await posts.SaveChangesAsync(ct);

        foreach (var key in storageKeys)
            await mediaStorage.DeleteAsync(key, ct);

        return JournalSaveResult.Ok();
    }

    // --- Admin: translations -----------------------------------------------------------------------

    public async Task<JournalSaveResult> SaveTranslationAsync(
        Guid postId, JournalPostTranslation translation, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var post = await posts.GetWithDetailAsync(postId, ct);
        if (post is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        if (!SiteCultures.IsSupported(translation.Culture))
            return JournalSaveResult.Fail("Journal.Error.UnknownCulture");

        var culture = SiteCultures.Normalise(translation.Culture);
        var body = EditorHtml.Sanitize(translation.Body);

        var existing = JournalContent.Find(post, culture);
        if (existing is null)
        {
            existing = new JournalPostTranslation { JournalPostId = post.Id, Culture = culture };
            await translations.AddAsync(existing, ct);
            // Also attached to the navigation, which SeminarService's equivalent does not need to
            // do. The prune below reads every body on the post to decide which files are still in
            // use, and a row that exists only in the change tracker is not one it can see — the
            // images in a brand-new German article would be deleted the moment it was saved.
            post.Translations.Add(existing);
        }
        else
        {
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        existing.Title = translation.Title.Trim();
        existing.Excerpt = string.IsNullOrWhiteSpace(translation.Excerpt) ? null : translation.Excerpt.Trim();
        existing.SeoTitle = string.IsNullOrWhiteSpace(translation.SeoTitle) ? null : translation.SeoTitle.Trim();
        existing.SeoDescription = string.IsNullOrWhiteSpace(translation.SeoDescription) ? null : translation.SeoDescription.Trim();
        existing.Body = body;

        post.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("JournalTranslationSaved", post.Id, adminUserId, ipAddress,
            before: null, after: new { existing.Culture, existing.Title }, ct);
        await posts.SaveChangesAsync(ct);

        await PruneInlineMediaAsync(post, adminUserId, ipAddress, ct);

        return JournalSaveResult.Ok(post.Id);
    }

    public async Task<JournalSaveResult> DeleteTranslationAsync(
        Guid postId, string culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var post = await posts.GetWithDetailAsync(postId, ct);
        if (post is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        // The default culture is what every other language falls back to; removing it would leave a
        // post that renders correctly in no language at all.
        if (string.Equals(culture, SiteCultures.Default, StringComparison.OrdinalIgnoreCase))
            return JournalSaveResult.Fail("Journal.Error.CannotDeleteDefaultCulture");

        var existing = JournalContent.Find(post, culture);
        if (existing is null) return JournalSaveResult.Ok(post.Id);

        translations.Remove(existing);
        post.Translations.Remove(existing);
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("JournalTranslationDeleted", post.Id, adminUserId, ipAddress,
            before: new { existing.Culture, existing.Title }, after: null, ct);
        await posts.SaveChangesAsync(ct);

        // The German article's images are nobody's once the German article is gone.
        await PruneInlineMediaAsync(post, adminUserId, ipAddress, ct);

        return JournalSaveResult.Ok(post.Id);
    }

    // --- Admin: media ------------------------------------------------------------------------------

    public async Task<JournalMediaResult> AddMediaAsync(
        Guid postId, MediaUpload upload, string? title, bool isInline,
        Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var post = await posts.GetWithDetailAsync(postId, ct);
        if (post is null) return JournalMediaResult.Fail("Journal.Error.NotFound");

        // Classified from the extension, not from the browser's declared content type — see
        // MediaPolicy. A null here means the file is simply not something we serve.
        var kind = MediaPolicy.Classify(upload.FileName);
        if (kind is null) return JournalMediaResult.Fail("Seminar.Error.MediaType");

        if (upload.Length > MediaPolicy.MaxBytesFor(kind.Value))
            return JournalMediaResult.Fail("Seminar.Error.MediaTooLarge");

        // The same file uploaded twice to one post is stored once. Scoped to the post on purpose:
        // every file belongs to exactly one post, which is what lets deleting a post take its files
        // with it. Sharing a file between posts would make that delete unsafe.
        await using var hashed = await FingerprintAsync(upload, ct);
        upload = hashed.Upload;
        var duplicate = post.Media.FirstOrDefault(m => m.ContentHash == hashed.Hash);
        if (duplicate is not null)
        {
            // A library upload of a picture that so far only sat inside the article moves it into
            // the library, where the admin now wants it. It also stops the prune step from ever
            // treating it as the article's leftover.
            if (!isInline && duplicate.IsInline)
            {
                duplicate.IsInline = false;
                if (!string.IsNullOrWhiteSpace(title)) duplicate.Title = title.Trim();
                post.UpdatedAt = DateTimeOffset.UtcNow;
                await LogAsync("JournalMediaPromoted", post.Id, adminUserId, ipAddress,
                    before: new { duplicate.Id, IsInline = true }, after: new { duplicate.Id, IsInline = false }, ct);
                await posts.SaveChangesAsync(ct);
            }

            return JournalMediaResult.Ok(duplicate, reused: true);
        }

        var saved = await mediaStorage.SaveAsync(upload, $"journal/{postId:N}", ct);
        if (!saved.Success) return JournalMediaResult.Fail(saved.Error ?? "Seminar.Error.MediaFailed");

        var media = new JournalPostMedia
        {
            JournalPostId = postId,
            StorageKey = saved.StorageKey!,
            Kind = kind.Value,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            IsInline = isInline,
            ContentType = saved.ContentType!,
            SizeBytes = saved.SizeBytes,
            ContentHash = hashed.Hash,
            OriginalFileName = upload.FileName,
            SortOrder = post.Media.Count == 0 ? 1 : post.Media.Max(m => m.SortOrder) + 1,
        };
        await mediaRows.AddAsync(media, ct);

        post.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("JournalMediaAdded", post.Id, adminUserId, ipAddress, before: null,
            after: new { media.StorageKey, Kind = kind.Value.ToString(), saved.SizeBytes, upload.FileName, isInline }, ct);

        try
        {
            await posts.SaveChangesAsync(ct);
        }
        catch
        {
            // The bytes are already on disk by this point. Without this, a failed save leaves a file
            // nothing references and nothing will ever clean up. Put back and rethrow.
            await mediaStorage.DeleteAsync(saved.StorageKey!, ct);
            throw;
        }

        return JournalMediaResult.Ok(media);
    }

    public async Task<JournalSaveResult> RemoveMediaAsync(
        Guid postId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var post = await posts.GetWithDetailAsync(postId, ct);
        if (post is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        var media = post.Media.FirstOrDefault(m => m.Id == mediaId);
        if (media is null) return JournalSaveResult.Ok(post.Id);

        await RemoveMediaRowAsync(post, media, "JournalMediaRemoved", adminUserId, ipAddress, ct);
        return JournalSaveResult.Ok(post.Id);
    }

    public async Task<JournalSaveResult> SaveGalleryAsync(
        Guid postId, IReadOnlyList<JournalGalleryEdit> items, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var post = await posts.GetWithDetailAsync(postId, ct);
        if (post is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        var library = post.Media.Where(m => !m.IsInline).ToDictionary(m => m.Id);
        var before = library.Values.OrderBy(m => m.SortOrder)
            .Select(m => new { m.Id, m.SortOrder, m.ShowInGallery, m.Captions }).ToList();

        // Ids from another post, inline assets or a double-submitted delete are ignored rather than
        // failing the whole save — the rest of what the admin arranged is still worth keeping.
        var listed = items.Where(i => library.ContainsKey(i.MediaId)).DistinctBy(i => i.MediaId).ToList();
        foreach (var item in listed)
        {
            var media = library[item.MediaId];
            JournalMediaCaptions.Write(media, item.Captions);
            media.ShowInGallery = item.ShowInGallery;
        }

        var order = listed.Select(i => library[i.MediaId])
            .Concat(library.Values.Where(m => listed.All(i => i.MediaId != m.Id)).OrderBy(m => m.SortOrder))
            .ToList();
        for (var i = 0; i < order.Count; i++)
            order[i].SortOrder = i + 1;

        // Inline assets sort after the library so a later upload's "max + 1" never lands between
        // two gallery photographs.
        var next = order.Count;
        foreach (var inline in post.Media.Where(m => m.IsInline).OrderBy(m => m.SortOrder))
            inline.SortOrder = ++next;

        post.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("JournalGallerySaved", post.Id, adminUserId, ipAddress, before,
            after: order.Select(m => new { m.Id, m.SortOrder, m.ShowInGallery, m.Captions }), ct);
        await posts.SaveChangesAsync(ct);

        return JournalSaveResult.Ok(post.Id);
    }

    /// <summary>
    /// The photographs shown in the gallery under the article, in the admin's order: library images
    /// and GIFs the admin has not hidden. Inline images and the cover are left out because the page
    /// already shows them.
    /// </summary>
    public static IEnumerable<JournalPostMedia> GalleryMedia(JournalPost post) =>
        post.Media
            .Where(m => !m.IsInline
                        && m.ShowInGallery
                        && m.Id != post.CoverMediaId
                        && m.Kind is SeminarMediaKind.Image or SeminarMediaKind.Animation)
            .OrderBy(m => m.SortOrder);

    public async Task<JournalSaveResult> SetCoverAsync(
        Guid postId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var post = await posts.GetWithDetailAsync(postId, ct);
        if (post is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        var media = post.Media.FirstOrDefault(m => m.Id == mediaId);
        if (media is null) return JournalSaveResult.Fail("Journal.Error.MediaNotFound");
        if (media.Kind != SeminarMediaKind.Image) return JournalSaveResult.Fail("Journal.Error.CoverMustBeImage");

        var before = new { post.CoverMediaId };
        post.CoverMediaId = mediaId;
        // An uploaded cover wins outright: two sources of truth for one <img src> means the next
        // person to read this has to guess which one is showing.
        post.CoverImageUrl = null;
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("JournalCoverChanged", post.Id, adminUserId, ipAddress, before, new { post.CoverMediaId }, ct);
        await posts.SaveChangesAsync(ct);

        return JournalSaveResult.Ok(post.Id);
    }

    public async Task<JournalSaveResult> ReplaceCoverAsync(
        Guid postId, MediaUpload upload, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var post = await posts.GetWithDetailAsync(postId, ct);
        if (post is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        if (MediaPolicy.Classify(upload.FileName) is not SeminarMediaKind.Image)
            return JournalSaveResult.Fail("Journal.Error.CoverMustBeImage");

        var added = await AddMediaAsync(postId, upload, title: null, isInline: false, adminUserId, ipAddress, ct);
        if (!added.Success || added.Media is null) return JournalSaveResult.Fail(added.Error ?? "Seminar.Error.MediaFailed");

        // Reload: AddMediaAsync committed, and `post` was loaded before the new row existed.
        var reloaded = await posts.GetWithDetailAsync(postId, ct);
        if (reloaded is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        var previous = reloaded.Media.FirstOrDefault(m => m.Id == reloaded.CoverMediaId);

        reloaded.CoverMediaId = added.Media.Id;
        reloaded.CoverImageUrl = null;
        reloaded.UpdatedAt = DateTimeOffset.UtcNow;
        await posts.SaveChangesAsync(ct);

        // Replacing means replacing: the old cover's row and its file go, rather than accumulating
        // in the library as a copy of a picture nothing shows. Only when it was not also used
        // inline somewhere in the article.
        // previous == the upload when the same cover was uploaded again: nothing to replace.
        if (previous is not null && previous.Id != added.Media.Id && !IsReferencedInAnyBody(reloaded, previous))
            await RemoveMediaRowAsync(reloaded, previous, "JournalCoverReplaced", adminUserId, ipAddress, ct);

        return JournalSaveResult.Ok(postId);
    }

    public async Task<JournalSaveResult> RemoveCoverAsync(
        Guid postId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var post = await posts.GetWithDetailAsync(postId, ct);
        if (post is null) return JournalSaveResult.Fail("Journal.Error.NotFound");

        var previous = post.Media.FirstOrDefault(m => m.Id == post.CoverMediaId);

        post.CoverMediaId = null;
        post.CoverImageUrl = null;
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("JournalCoverRemoved", post.Id, adminUserId, ipAddress,
            before: new { previous?.StorageKey }, after: null, ct);
        await posts.SaveChangesAsync(ct);

        if (previous is not null && !IsReferencedInAnyBody(post, previous))
            await RemoveMediaRowAsync(post, previous, "JournalMediaRemoved", adminUserId, ipAddress, ct);

        return JournalSaveResult.Ok(post.Id);
    }

    // --- Helpers -------------------------------------------------------------------------------------

    /// <summary>
    /// Drops one media row and then its file, clearing the cover pointer first if it named this
    /// row — a dangling CoverMediaId renders as no cover, but it also survives in the database
    /// forever and makes every later read wonder what it pointed at.
    /// </summary>
    private async Task RemoveMediaRowAsync(
        JournalPost post, JournalPostMedia media, string action, Guid adminUserId, string? ipAddress, CancellationToken ct)
    {
        if (post.CoverMediaId == media.Id) post.CoverMediaId = null;

        var storageKey = media.StorageKey;
        mediaRows.Remove(media);
        post.Media.Remove(media);
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync(action, post.Id, adminUserId, ipAddress,
            before: new { media.StorageKey, Kind = media.Kind.ToString(), media.IsInline }, after: null, ct);
        await posts.SaveChangesAsync(ct);

        await mediaStorage.DeleteAsync(storageKey, ct);
    }

    /// <summary>
    /// Deletes every inline asset that no language's body mentions any more.
    ///
    /// This is what stops the media directory filling up with the images of every draft that was
    /// rewritten. Only inline assets are eligible: an attachment in the library is there because
    /// somebody put it there, and the fact that no body links to it yet is not evidence it is
    /// unwanted.
    ///
    /// Every translation is checked, not just the one being saved — an image used only in the German
    /// article must survive an edit to the English one.
    /// </summary>
    private async Task PruneInlineMediaAsync(JournalPost post, Guid adminUserId, string? ipAddress, CancellationToken ct)
    {
        var orphans = post.Media
            .Where(m => m.IsInline && m.Id != post.CoverMediaId && !IsReferencedInAnyBody(post, m))
            .ToList();

        foreach (var orphan in orphans)
            await RemoveMediaRowAsync(post, orphan, "JournalMediaPruned", adminUserId, ipAddress, ct);
    }

    private static bool IsReferencedInAnyBody(JournalPost post, JournalPostMedia media)
    {
        var url = MediaUrl(media.Id);
        return post.Translations.Any(t => t.Body.Contains(url, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// SHA-256 of an upload, with the upload handed back ready to be read again from the start. A
    /// stream that cannot rewind is first copied to a temporary file that is deleted when the
    /// result is disposed.
    /// </summary>
    private static async Task<Fingerprint> FingerprintAsync(MediaUpload upload, CancellationToken ct)
    {
        var source = upload.Content;
        FileStream? spill = null;
        if (!source.CanSeek)
        {
            spill = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await source.CopyToAsync(spill, ct);
            source = spill;
        }

        source.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, ct));
        source.Position = 0;

        return new Fingerprint(hash, upload with { Content = source }, spill);
    }

    private sealed record Fingerprint(string Hash, MediaUpload Upload, Stream? Spill) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Spill?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(JournalPost),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
