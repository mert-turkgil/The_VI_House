using System.ComponentModel.DataAnnotations;
using VIHouse.Entities.Users;

namespace VIHouse.WebUI.ViewModels.Account;

/// <summary>Deliberately just the "Basic Profile" fields (brief §206) — the fuller member-directory
/// profile (photo, interests, looking-for/can-help-with, visibility) is Phase 2 scope.</summary>
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

    public static ProfileFormViewModel FromEntity(Profile profile) => new()
    {
        CompanyName = profile.CompanyName,
        JobTitle = profile.JobTitle,
        Industry = profile.Industry,
        Bio = profile.Bio,
        LinkedInUrl = profile.LinkedInUrl,
        WebsiteUrl = profile.WebsiteUrl,
    };
}
