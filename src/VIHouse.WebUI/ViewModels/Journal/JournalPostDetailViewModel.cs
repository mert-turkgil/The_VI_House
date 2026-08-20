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
    public string? AuthorName { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Sanitised HTML from the admin's rich text editor, rendered with Html.Raw. Safe to
    /// trust here because JournalService sanitises on every write — see JournalHtml.</summary>
    public string BodyHtml { get; set; } = string.Empty;

    public string CategoryLabel => Category.ToDisplayLabel();

    public static JournalPostDetailViewModel FromEntity(JournalPost p) => new()
    {
        Title = p.Title,
        Slug = p.Slug,
        Category = p.Category,
        CoverImageUrl = p.CoverImageUrl,
        AuthorName = p.AuthorName,
        PublishedAt = p.PublishedAt,
        // EnsureHtml covers posts written before the editor existed, whose bodies are still stored
        // as blank-line-separated plain text and would otherwise render as one run-on paragraph.
        BodyHtml = JournalHtml.EnsureHtml(p.Body),
    };
}
