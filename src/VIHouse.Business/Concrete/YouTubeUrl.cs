using System.Text.RegularExpressions;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Recognises a YouTube video URL and pulls out its id.
///
/// One implementation, used twice and for two different purposes: <see cref="EditorHtml"/> calls it
/// on write to refuse an embed that is not YouTube at all, and <see cref="ArticleHtml"/> calls it on
/// read to build the player URL. Both must agree on what counts — an id one of them accepts and the
/// other does not is either a stored embed that never renders or a rendered embed that was never
/// checked.
/// </summary>
public static partial class YouTubeUrl
{
    /// <summary>
    /// Video ids are 11 characters of URL-safe base64. Anchored at both ends, so a query string, a
    /// path segment or anything else after the id has to be matched explicitly rather than
    /// swallowed — which is what stops "youtu.be/ID/../../evil" being read as a bare id.
    /// </summary>
    [GeneratedRegex(
        @"^(?:https?://)?(?:www\.|m\.)?(?:youtube\.com/(?:watch\?(?:[^#]*&)?v=|embed/|v/|shorts/)|youtu\.be/)(?<id>[\w-]{11})(?:[?&#/].*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern { get; }

    /// <summary>True when <paramref name="url"/> names a YouTube video, with its id in <paramref name="id"/>.</summary>
    public static bool TryParseId(string? url, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(url)) return false;

        var match = Pattern.Match(url.Trim());
        if (!match.Success) return false;

        id = match.Groups["id"].Value;
        return true;
    }

    /// <summary>The canonical watch URL, which is what gets stored — so the same video pasted from a
    /// share sheet, an embed code and the address bar all end up as one string.</summary>
    public static string WatchUrl(string id) => $"https://www.youtube.com/watch?v={id}";

    /// <summary>The privacy-preserving player origin. Only ever built from a parsed id.</summary>
    public static string EmbedUrl(string id) => $"https://www.youtube-nocookie.com/embed/{id}";

    /// <summary>Video still, served by Google's image CDN.</summary>
    public static string ThumbnailUrl(string id) => $"https://i.ytimg.com/vi/{id}/hqdefault.jpg";
}
