using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Community;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminCommunityLinkFormViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(120)]
    [Display(Name = "Admin.Field.Label", Description = "What members see, e.g. \"The VI House Discord\".")]
    public string Label { get; set; } = default!;

    [StringLength(400)]
    [Display(Name = "Admin.Field.Description")]
    public string? Description { get; set; }

    [Required, StringLength(500)]
    [Url(ErrorMessage = "Admin.Validation.EnterAFullURLIncludingHttps")]
    [Display(Name = "Admin.Field.URL")]
    public string Url { get; set; } = default!;

    [Display(Name = "Admin.Field.Kind")]
    public CommunityLinkKind Kind { get; set; } = CommunityLinkKind.Discord;

    [Display(Name = "Admin.Field.VisibleToMembers", Description = "Untick to hide a revoked invite without deleting it.")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Admin.Field.SortOrder")]
    public int SortOrder { get; set; }

    // --- Who sees it: one of these at most; all blank = every member with community access -------

    [Display(Name = "Admin.Field.OnlyForMembersOfPlan")]
    public Guid? MembershipPlanId { get; set; }

    [Display(Name = "Admin.Field.OnlyForTicketHoldersOfExperience")]
    public Guid? ExperienceId { get; set; }

    [Display(Name = "Admin.Field.OnlyForPeopleEnrolledOnSession")]
    public Guid? SeminarId { get; set; }

    [StringLength(40)]
    [Display(Name = "Admin.Field.DiscordChannelId")]
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
