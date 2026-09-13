namespace VIHouse.Entities.Users;

/// <summary>
/// 1:1 shadow of the Identity user (ApplicationUser lives in VIHouse.DataAccess).
/// Deliberately does NOT carry a navigation property back to ApplicationUser: Entities must stay
/// free of any dependency on Identity/DataAccess types, so the relationship is joined by UserId
/// wherever both are needed, and the FK constraint itself is configured in VIHouseDbContext.
///
/// Holds what the House asks of everyone, once — the same set of questions whether they arrived
/// through an application, a membership purchase, or were invited. Name, city and country live on
/// the Identity user itself; everything else about a person is here. The two statements
/// (<see cref="About"/>, <see cref="Expectations"/>) are the required pair; the rest is optional.
/// </summary>
public class Profile
{
    public Guid UserId { get; set; }

    /// <summary>Title / profession — "Founder", "Partner, XYZ Capital", "Architect".</summary>
    public string? JobTitle { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }

    /// <summary>"Describe yourself or your business."</summary>
    public string? About { get; set; }

    /// <summary>"How may we help you with your goals, or what are your expectations?"</summary>
    public string? Expectations { get; set; }

    /// <summary>One of <see cref="Users.EarningsBand"/>'s codes.</summary>
    public string? EarningsBand { get; set; }

    public string? PhotoUrl { get; set; }
    public ProfileVisibility Visibility { get; set; } = ProfileVisibility.MembersOnly;
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// Whether the required answers are in. This is what the session enrolment gate and the
    /// account dashboard's "complete your profile" prompt both read, so the definition of
    /// "complete" lives in exactly one place.
    /// </summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(About) && !string.IsNullOrWhiteSpace(Expectations);
}
