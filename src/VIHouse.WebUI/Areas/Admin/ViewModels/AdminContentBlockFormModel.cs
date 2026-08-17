using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>Shared by both "update an existing block" and "add a new block" — Id is empty Guid for Add.</summary>
public class AdminContentBlockFormModel
{
    public Guid Id { get; set; }
    public Guid PageId { get; set; }
    public string PageSlug { get; set; } = default!;

    [Required, StringLength(60)]
    public string SectionKey { get; set; } = default!;

    public int SortOrder { get; set; }
    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public string? BodyText { get; set; }
    public string? ImageUrl { get; set; }
    public string? CtaLabel { get; set; }
    public string? CtaUrl { get; set; }
    public string? ExtraJson { get; set; }
}
