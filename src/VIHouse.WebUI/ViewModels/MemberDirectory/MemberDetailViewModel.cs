using VIHouse.DataAccess.Abstract;

namespace VIHouse.WebUI.ViewModels.MemberDirectory;

public class MemberDetailViewModel
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string? JobTitle { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = default!;
    public string? PhotoUrl { get; set; }
    public string? About { get; set; }

    public static MemberDetailViewModel FromEntry(MemberDirectoryEntry e) => new()
    {
        UserId = e.UserId,
        FullName = $"{e.FirstName} {e.LastName}",
        JobTitle = e.JobTitle,
        City = e.City,
        Country = e.Country,
        PhotoUrl = e.PhotoUrl,
        About = e.About,
    };
}
