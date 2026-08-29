using VIHouse.Business.Concrete;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.ViewModels.Journal;

public class JournalPostCardViewModel
{
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public JournalCategory Category { get; set; }
    public string? Excerpt { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    public string CategoryLabel => Category.ToDisplayLabel();

    /// <param name="culture">The reader's culture. A post with no copy in it falls back through
    /// JournalContent rather than rendering a card with no headline.</param>
    public static JournalPostCardViewModel FromEntity(JournalPost p, string? culture)
    {
        var copy = JournalContent.Resolve(p, culture);

        return new JournalPostCardViewModel
        {
            Title = copy?.Title ?? p.Slug,
            Slug = p.Slug,
            Category = p.Category,
            Excerpt = copy?.Excerpt,
            CoverImageUrl = JournalPostDetailViewModel.CoverUrl(p),
            AuthorName = p.AuthorName,
            PublishedAt = p.PublishedAt,
        };
    }
}
