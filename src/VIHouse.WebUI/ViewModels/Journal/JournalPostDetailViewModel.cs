using System.Text.RegularExpressions;
using VIHouse.Business.Concrete;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.ViewModels.Journal;

public class JournalPostDetailViewModel
{
    public string Title { get; set; } = default!;

    /// <summary>The post's standfirst, shown under the headline. Null hides it.</summary>
    public string? Excerpt { get; set; }

    /// <summary>Whole minutes at an unhurried reading pace, never less than one.</summary>
    public int ReadingMinutes { get; set; } = 1;
    public string Slug { get; set; } = default!;
    public JournalCategory Category { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? CoverImageAlt { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Sanitised HTML from the admin's rich text editor, rendered with Html.Raw. Safe to
    /// trust here because JournalService sanitises on every write — see EditorHtml.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>The photographs under the article, in the admin's order, captions already in the
    /// reader's language. Empty hides the section.</summary>
    public List<JournalGalleryItem> Gallery { get; set; } = [];

    public string CategoryLabel => Category.ToDisplayLabel();

    /// <summary>Set under an influencer's article (JournalController).</summary>
    public JournalAuthorBox? Author { get; set; }

    /// <param name="culture">The reader's culture, from CurrentUICulture — the same source the
    /// .resx strings around the article use, so the copy can never be in a different language from
    /// the chrome surrounding it.</param>
    /// <param name="videoPlayLabel">Localised accessible name for a video's play button. Passed in
    /// because ArticleHtml lives in Business, which has no localiser and should not grow one.</param>
    /// <param name="viewOnFormat">"View on {0}" for social link cards, localised for the same reason.</param>
    public static JournalPostDetailViewModel FromEntity(JournalPost p, string? culture, string videoPlayLabel, string viewOnFormat)
    {
        var copy = JournalContent.Resolve(p, culture);

        return new JournalPostDetailViewModel
        {
            Title = copy?.Title ?? p.Slug,
            Excerpt = string.IsNullOrWhiteSpace(copy?.Excerpt) ? null : copy.Excerpt.Trim(),
            ReadingMinutes = ReadingMinutesFor(copy?.Body),
            Slug = p.Slug,
            Category = p.Category,
            CoverImageUrl = CoverUrl(p),
            CoverImageAlt = p.CoverImageAlt,
            AuthorName = p.AuthorName,
            PublishedAt = p.PublishedAt,
            // EnsureHtml covers posts written before the editor existed, whose bodies are still
            // stored as blank-line-separated plain text and would otherwise render as one run-on
            // paragraph. RenderForDisplay then turns any video marker into its click-to-play facade.
            BodyHtml = ArticleHtml.RenderForDisplay(EditorHtml.EnsureHtml(copy?.Body ?? string.Empty), videoPlayLabel, viewOnFormat),
            Gallery = [.. JournalService.GalleryMedia(p)
                .Select(m => new JournalGalleryItem(JournalService.MediaUrl(m.Id), JournalMediaCaptions.Resolve(m, culture)))],
        };
    }

    // 220 words a minute: slower than skimming, which is how an essay like these is read.
    private const int WordsPerMinute = 220;

    private static int ReadingMinutesFor(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return 1;
        var text = Regex.Replace(body, "<[^>]+>", " ");
        var words = Regex.Matches(text, @"[\p{L}\p{N}]+(?:['’][\p{L}]+)?").Count;
        return Math.Max(1, (int)Math.Round(words / (double)WordsPerMinute));
    }

    /// <summary>An uploaded cover is streamed by MediaController; a pasted one is used as written.
    /// Shared with the card view model, so the two can never disagree about which wins.</summary>
    internal static string? CoverUrl(JournalPost p) =>
        p.CoverMediaId is { } mediaId ? JournalService.MediaUrl(mediaId) : p.CoverImageUrl;
}

/// <summary>One photograph in the article's gallery.</summary>
public record JournalGalleryItem(string Url, string? Caption);

/// <param name="JoinUrl">The influencer's /r/{code} link; null while their links are switched off.</param>
public record JournalAuthorBox(string Name, string? PhotoUrl, string? Bio, string? Niche,
    IReadOnlyList<VIHouse.Entities.Referrals.AmbassadorChannel> Channels, string? JoinUrl);
