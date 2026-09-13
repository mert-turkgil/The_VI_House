using VIHouse.DataAccess.Abstract;

namespace VIHouse.WebUI.ViewModels.MemberDirectory;

public class MemberCardViewModel
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string? JobTitle { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public string? AboutSnippet { get; set; }

    public static MemberCardViewModel FromEntry(MemberDirectoryEntry e) => new()
    {
        UserId = e.UserId,
        FullName = $"{e.FirstName} {e.LastName}",
        JobTitle = e.JobTitle,
        City = e.City,
        Country = e.Country,
        PhotoUrl = e.PhotoUrl,
        AboutSnippet = string.IsNullOrWhiteSpace(e.About) ? null : (e.About.Length > 140 ? e.About[..140] + "…" : e.About),
    };
}
