using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Journal;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminJournalPostFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = default!;

    [Required, StringLength(200)]
    public string Slug { get; set; } = default!;

    [Required]
    public JournalCategory Category { get; set; }

    [Required]
    public JournalPostStatus Status { get; set; } = JournalPostStatus.Draft;

    [StringLength(500)]
    public string? Excerpt { get; set; }

    [Required]
    public string Body { get; set; } = default!;

    public string? CoverImageUrl { get; set; }

    [StringLength(150)]
    public string? AuthorName { get; set; }

    public JournalPost ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Title = Title.Trim(),
        Slug = Slug.Trim(),
        Category = Category,
        Status = Status,
        Excerpt = Excerpt,
        Body = Body,
        CoverImageUrl = CoverImageUrl,
        AuthorName = AuthorName,
    };

    public static AdminJournalPostFormViewModel FromEntity(JournalPost p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Slug = p.Slug,
        Category = p.Category,
        Status = p.Status,
        Excerpt = p.Excerpt,
        Body = p.Body,
        CoverImageUrl = p.CoverImageUrl,
        AuthorName = p.AuthorName,
    };
}
