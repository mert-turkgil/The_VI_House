using VIHouse.Business.Concrete;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.ViewModels.Journal;

public class JournalPostDetailViewModel
{
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public JournalCategory Category { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? CoverImageAlt { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Sanitised HTML from the admin's rich text editor, rendered with Html.Raw. Safe to
    /// trust here because JournalService sanitises on every write — see EditorHtml.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    public string CategoryLabel => Category.ToDisplayLabel();

    /// <param name="culture">The reader's culture, from CurrentUICulture — the same source the
    /// .resx strings around the article use, so the copy can never be in a different language from
    /// the chrome surrounding it.</param>
    /// <param name="videoPlayLabel">Localised accessible name for a video's play button. Passed in
    /// because ArticleHtml lives in Business, which has no localiser and should not grow one.</param>
    public static JournalPostDetailViewModel FromEntity(JournalPost p, string? culture, string videoPlayLabel)
    {
        var copy = JournalContent.Resolve(p, culture);

        return new JournalPostDetailViewModel
        {
            Title = copy?.Title ?? p.Slug,
            Slug = p.Slug,
            Category = p.Category,
            CoverImageUrl = CoverUrl(p),
            CoverImageAlt = p.CoverImageAlt,
            AuthorName = p.AuthorName,
            PublishedAt = p.PublishedAt,
            // EnsureHtml covers posts written before the editor existed, whose bodies are still
            // stored as blank-line-separated plain text and would otherwise render as one run-on
            // paragraph. RenderForDisplay then turns any video marker into its click-to-play facade.
            BodyHtml = ArticleHtml.RenderForDisplay(EditorHtml.EnsureHtml(copy?.Body ?? string.Empty), videoPlayLabel),
        };
    }

    /// <summary>An uploaded cover is streamed by MediaController; a pasted one is used as written.
    /// Shared with the card view model, so the two can never disagree about which wins.</summary>
    internal static string? CoverUrl(JournalPost p) =>
        p.CoverMediaId is { } mediaId ? JournalService.MediaUrl(mediaId) : p.CoverImageUrl;
}
