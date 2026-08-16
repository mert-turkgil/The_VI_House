namespace VIHouse.Entities.Users;

/// <summary>
/// 1:1 shadow of the Identity user (ApplicationUser lives in VIHouse.DataAccess).
/// Deliberately does NOT carry a navigation property back to ApplicationUser: Entities must stay
/// free of any dependency on Identity/DataAccess types, so the relationship is joined by UserId
/// wherever both are needed, and the FK constraint itself is configured in VIHouseDbContext.
/// </summary>
public class Profile
{
    public Guid UserId { get; set; }

    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Industry { get; set; }
    public string? Bio { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? InstagramHandle { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? Interests { get; set; }
    public string? LookingFor { get; set; }
    public string? CanHelpWith { get; set; }
    public string? PhotoUrl { get; set; }
    public ProfileVisibility Visibility { get; set; } = ProfileVisibility.MembersOnly;
    public DateTimeOffset? UpdatedAt { get; set; }
}
