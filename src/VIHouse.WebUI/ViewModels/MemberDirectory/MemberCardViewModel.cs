using VIHouse.DataAccess.Abstract;

namespace VIHouse.WebUI.ViewModels.MemberDirectory;

public class MemberCardViewModel
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Industry { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public string? BioSnippet { get; set; }

    public static MemberCardViewModel FromEntry(MemberDirectoryEntry e) => new()
    {
        UserId = e.UserId,
        FullName = $"{e.FirstName} {e.LastName}",
        CompanyName = e.CompanyName,
        JobTitle = e.JobTitle,
        Industry = e.Industry,
        City = e.City,
        Country = e.Country,
        PhotoUrl = e.PhotoUrl,
        BioSnippet = string.IsNullOrWhiteSpace(e.Bio) ? null : (e.Bio.Length > 140 ? e.Bio[..140] + "…" : e.Bio),
    };
}
