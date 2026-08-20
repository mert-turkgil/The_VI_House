using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Community;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminCommunityLinkFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(120)]
    [Display(Name = "Label", Description = "What members see, e.g. \"The VI House Discord\".")]
    public string Label { get; set; } = default!;

    [StringLength(400)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required, StringLength(500)]
    [Url(ErrorMessage = "Enter a full URL, including https://")]
    [Display(Name = "URL")]
    public string Url { get; set; } = default!;

    [Display(Name = "Kind")]
    public CommunityLinkKind Kind { get; set; } = CommunityLinkKind.Discord;

    [Display(Name = "Visible to members", Description = "Untick to hide a revoked invite without deleting it.")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }

    public CommunityLink ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Label = Label.Trim(),
        Description = Description?.Trim(),
        Url = Url.Trim(),
        Kind = Kind,
        IsActive = IsActive,
        SortOrder = SortOrder,
    };

    public static AdminCommunityLinkFormViewModel FromEntity(CommunityLink l) => new()
    {
        Id = l.Id,
        Label = l.Label,
        Description = l.Description,
        Url = l.Url,
        Kind = l.Kind,
        IsActive = l.IsActive,
        SortOrder = l.SortOrder,
    };
}
