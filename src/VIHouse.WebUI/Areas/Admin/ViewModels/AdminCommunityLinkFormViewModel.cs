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

    // --- Who sees it: one of these at most; all blank = every member with community access -------

    [Display(Name = "Only for members of plan")]
    public Guid? MembershipPlanId { get; set; }

    [Display(Name = "Only for ticket holders of experience")]
    public Guid? ExperienceId { get; set; }

    [Display(Name = "Only for people enrolled on session")]
    public Guid? SeminarId { get; set; }

    [StringLength(40)]
    [Display(Name = "Discord channel id")]
    public string? DiscordChannelId { get; set; }

    /// <summary>A link belongs to one thing at most — a channel that is "for plan A and for
    /// experience B" has no clear audience, so the form refuses it rather than guessing.</summary>
    public string? ScopeError() =>
        new[] { MembershipPlanId, ExperienceId, SeminarId }.Count(x => x is not null) > 1
            ? "Choose one audience: a plan, an experience or a session — not several."
            : null;

    public CommunityLink ToEntity() => new()
    {
        Id = Id ?? Guid.NewGuid(),
        Label = Label.Trim(),
        Description = Description?.Trim(),
        Url = Url.Trim(),
        Kind = Kind,
        IsActive = IsActive,
        SortOrder = SortOrder,
        MembershipPlanId = MembershipPlanId,
        ExperienceId = ExperienceId,
        SeminarId = SeminarId,
        DiscordChannelId = string.IsNullOrWhiteSpace(DiscordChannelId) ? null : DiscordChannelId.Trim(),
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
        MembershipPlanId = l.MembershipPlanId,
        ExperienceId = l.ExperienceId,
        SeminarId = l.SeminarId,
        DiscordChannelId = l.DiscordChannelId,
    };
}
