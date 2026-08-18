using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Users;

namespace VIHouse.WebUI.ViewModels.Account;

public class ProfileFormViewModel
{
    [StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(150)]
    public string? JobTitle { get; set; }

    [StringLength(100)]
    public string? Industry { get; set; }

    [StringLength(1000)]
    public string? Bio { get; set; }

    [Url, StringLength(300)]
    public string? LinkedInUrl { get; set; }

    [Url, StringLength(300)]
    public string? WebsiteUrl { get; set; }

    // The three fields below only ever surface on the Member Directory profile card/detail page —
    // added alongside the Directory itself (brief §38) rather than with the rest of "Basic Profile",
    // since until the Directory existed there was nothing that read them.
    [StringLength(300)]
    public string? Interests { get; set; }

    [StringLength(300)]
    public string? LookingFor { get; set; }

    [StringLength(300)]
    public string? CanHelpWith { get; set; }

    /// <summary>true = listed in the Member Directory (ProfileVisibility.MembersOnly), false = hidden (ProfileVisibility.Private). EventParticipants isn't offered here — nothing consumes it yet (see Event Attendee Directory, brief §41, not built).</summary>
    public bool VisibleInDirectory { get; set; } = true;

    public static ProfileFormViewModel FromEntity(Profile profile) => new()
    {
        CompanyName = profile.CompanyName,
        JobTitle = profile.JobTitle,
        Industry = profile.Industry,
        Bio = profile.Bio,
        LinkedInUrl = profile.LinkedInUrl,
        WebsiteUrl = profile.WebsiteUrl,
        Interests = profile.Interests,
        LookingFor = profile.LookingFor,
        CanHelpWith = profile.CanHelpWith,
        VisibleInDirectory = profile.Visibility == ProfileVisibility.MembersOnly,
    };
}
