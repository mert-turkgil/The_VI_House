using VIHouse.DataAccess.Abstract;

namespace VIHouse.WebUI.ViewModels.MemberDirectory;

public class MemberDetailViewModel
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Industry { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public string? Bio { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? Interests { get; set; }
    public string? LookingFor { get; set; }
    public string? CanHelpWith { get; set; }

    public static MemberDetailViewModel FromEntry(MemberDirectoryEntry e) => new()
    {
        UserId = e.UserId,
        FullName = $"{e.FirstName} {e.LastName}",
        CompanyName = e.CompanyName,
        JobTitle = e.JobTitle,
        Industry = e.Industry,
        City = e.City,
        Country = e.Country,
        PhotoUrl = e.PhotoUrl,
        Bio = e.Bio,
        LinkedInUrl = e.LinkedInUrl,
        Interests = e.Interests,
        LookingFor = e.LookingFor,
        CanHelpWith = e.CanHelpWith,
    };
}
