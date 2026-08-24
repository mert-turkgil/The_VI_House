using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Services;

/// <summary>
/// Stores uploaded seminar assets on the local filesystem, under the media root configured by
/// <see cref="MediaOptions"/>.
///
/// Lives in WebUI rather than Business for the same reason RazorEmailTemplateRenderer does: it
/// needs the hosting environment, and Business must not take a dependency on ASP.NET Core.
///
/// The root is deliberately not inside wwwroot and is never mapped into the static-file pipeline.
/// Two reasons: a priced, members-only seminar's recording must not be fetchable by anyone who
/// receives the link, and MapStaticAssets only serves files that existed at build time anyway, so a
/// runtime upload under wwwroot would 404 in Production while appearing to work in Development.
/// Everything is streamed by SeminarsController after an enrolment check instead.
///
/// The security posture is "the client controls nothing about where the bytes land". The stored
/// name is a fresh GUID, the extension comes from <see cref="MediaPolicy"/>'s allow-list rather than
/// from the upload, the content type is the one that allow-list maps the extension to (never the
/// browser's Content-Type header, which is a claim rather than a fact), and every resolved path is
/// checked to still be inside the root before anything is read, written or deleted.
/// </summary>
public class LocalMediaStorage(IOptions<MediaOptions> options, ILogger<LocalMediaStorage> logger) : IMediaStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<MediaSaveResult> SaveAsync(MediaUpload upload, string folder, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(upload.FileName);
        if (string.IsNullOrEmpty(extension) || !MediaPolicy.AllowedTypes.TryGetValue(extension, out var contentType))
            return MediaSaveResult.Fail("Seminar.Error.MediaType");

        if (upload.Length <= 0)
            return MediaSaveResult.Fail("Seminar.Error.MediaEmpty");

        if (upload.Length > MediaPolicy.MaxUploadBytes)
            return MediaSaveResult.Fail("Seminar.Error.MediaTooLarge");

        // The folder is composed by SeminarService from a GUID, not by a user — but it is still
        // concatenated into a path, so it is sanitised here rather than trusted. One careless caller
        // added later should not be able to write into the application directory.
        var safeFolder = SanitiseKey(folder);
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var storageKey = $"{safeFolder}/{storedName}";

        if (!TryResolve(storageKey, out var fullPath))
            return MediaSaveResult.Fail("Seminar.Error.MediaFailed");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            // FileMode.CreateNew, not Create: the name is a fresh GUID, so an existing file means
            // something is badly wrong and silently overwriting it is the worst available option.
            await using (var destination = new FileStream(
                fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await upload.Content.CopyToAsync(destination, ct);
            }

            return MediaSaveResult.Ok(storageKey, contentType, new FileInfo(fullPath).Length);
        }
        catch (Exception ex)
        {
            // A full disk or a permissions problem is an operational fault, not something the admin
            // did wrong — logged with detail, reported to them as a plain "that didn't save".
            logger.LogError(ex, "Failed to store uploaded media {FileName} under {Folder}.", upload.FileName, safeFolder);
            return MediaSaveResult.Fail("Seminar.Error.MediaFailed");
        }
    }

    public Task<MediaFileInfo?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        if (!TryResolve(storageKey, out var fullPath) || !File.Exists(fullPath))
            return Task.FromResult<MediaFileInfo?>(null);

        var extension = Path.GetExtension(fullPath);
        if (!MediaPolicy.AllowedTypes.TryGetValue(extension, out var contentType))
        {
            // A file whose extension is no longer on the allow-list is not served at all, rather
            // than served as application/octet-stream. If the policy tightened after an upload, the
            // safe reading is that the old file should stop being reachable.
            logger.LogWarning("Refusing to serve media with a non-allowed extension: {Key}", storageKey);
            return Task.FromResult<MediaFileInfo?>(null);
        }

        return Task.FromResult<MediaFileInfo?>(
            new MediaFileInfo(fullPath, contentType, new FileInfo(fullPath).Length));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        if (!TryResolve(storageKey, out var fullPath))
        {
            logger.LogWarning("Refused to delete media outside the media root: {Key}", storageKey);
            return Task.CompletedTask;
        }

        try
        {
            if (File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            // Deliberately swallowed: an orphaned file on disk is untidy, whereas failing here would
            // block deleting the row that points at it and leave a dead asset on the page.
            logger.LogWarning(ex, "Could not delete media file {Path}.", fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Turns a storage key into an absolute path, or refuses. Sanitising the key and then checking
    /// the fully resolved result against the fully resolved root is belt and braces on purpose:
    /// the first stops "..", the second stops anything the first did not think of, including a
    /// symlink pointing out of the tree.
    /// </summary>
    private bool TryResolve(string storageKey, out string fullPath)
    {
        fullPath = string.Empty;

        var safe = SanitiseKey(storageKey);
        if (safe.Length == 0) return false;

        var candidate = Path.GetFullPath(Path.Combine(_root, safe.Replace('/', Path.DirectorySeparatorChar)));

        var rootWithSeparator = _root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) return false;

        fullPath = candidate;
        return true;
    }

    /// <summary>
    /// Reduces a key to safe path segments: no drive letters, no "..", no absolute roots, nothing
    /// but ASCII letters, digits, dashes, underscores and dots, joined by forward slashes.
    /// </summary>
    private static string SanitiseKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return string.Empty;

        var segments = key
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => new string([.. segment.Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')]))
            .Where(segment => segment.Length > 0 && segment.Trim('.').Length > 0);

        return string.Join('/', segments);
    }
}
