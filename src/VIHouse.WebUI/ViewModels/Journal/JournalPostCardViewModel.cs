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

    public static JournalPostCardViewModel FromEntity(JournalPost p) => new()
    {
        Title = p.Title,
        Slug = p.Slug,
        Category = p.Category,
        Excerpt = p.Excerpt,
        CoverImageUrl = p.CoverImageUrl,
        AuthorName = p.AuthorName,
        PublishedAt = p.PublishedAt,
    };
}
