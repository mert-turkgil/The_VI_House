using System.ComponentModel.DataAnnotations;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Users;

namespace VIHouse.WebUI.ViewModels.Account;

/// <summary>
/// The account profile — the House's one set of questions, editable after the fact. Name, city
/// and country write through to the Identity user; the rest to the Profile row. The two
/// statements are the required pair, matching the application and join forms, so a profile that
/// passes here is also one that clears the session-enrolment gate.
/// </summary>
public class ProfileFormViewModel
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = default!;

    [Required, StringLength(100)]
    public string LastName { get; set; } = default!;

    [StringLength(200)]
    public string? JobTitle { get; set; }

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [Required(ErrorMessage = "Choose your country.")]
    [StringLength(2, MinimumLength = 2)]
    public string Country { get; set; } = default!;

    [Required(ErrorMessage = "Tell us a little about yourself or your business.")]
    [StringLength(2000)]
    public string About { get; set; } = default!;

    [Required(ErrorMessage = "Tell us what you're hoping for.")]
    [StringLength(2000)]
    public string Expectations { get; set; } = default!;

    [StringLength(20)]
    public string? EarningsBand { get; set; }

    /// <summary>true = listed in the Member Directory (ProfileVisibility.MembersOnly), false = hidden (ProfileVisibility.Private). EventParticipants isn't offered here — nothing consumes it yet (see Event Attendee Directory, brief §41, not built).</summary>
    public bool VisibleInDirectory { get; set; } = true;

    /// <summary>Where to go after a successful save — set when the member was sent here to complete
    /// their profile before enrolling in a session. Validated as a local URL before use.</summary>
    public string? ReturnUrl { get; set; }

    public static ProfileFormViewModel FromEntity(ApplicationUser user, Profile? profile) => new()
    {
        FirstName = user.FirstName,
        LastName = user.LastName,
        City = user.City,
        Country = user.Country,
        JobTitle = profile?.JobTitle,
        AddressLine1 = profile?.AddressLine1,
        AddressLine2 = profile?.AddressLine2,
        PostalCode = profile?.PostalCode,
        About = profile?.About ?? "",
        Expectations = profile?.Expectations ?? "",
        EarningsBand = profile?.EarningsBand,
        VisibleInDirectory = profile is null || profile.Visibility == ProfileVisibility.MembersOnly,
    };
}
