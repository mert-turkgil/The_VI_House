using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

/// <summary>
/// Creating a staff account. No password field by design — the new admin sets their own via a
/// one-time link, so a credential is never chosen on someone else's behalf, typed into a shared
/// screen, or sent over email.
/// </summary>
public class AdminInviteViewModel
{
    [Required, StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = default!;

    [Required, StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = default!;

    [Required, EmailAddress, StringLength(256)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = default!;

    /// <summary>Optional. ApplicationUser.Country is non-nullable because it's required of members
    /// at signup, but it carries no meaning for a staff account — left blank it's stored empty
    /// rather than guessed at.</summary>
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Use the two-letter country code, e.g. GB.")]
    [Display(Name = "Country (optional)")]
    public string? Country { get; set; }

    [Display(Name = "Roles")]
    public string[] Roles { get; set; } = [];

    /// <summary>Shown after a successful invite so the inviter can pass the link on directly if the
    /// email is slow or bounces — the same link that was emailed, not a second one.</summary>
    public string? IssuedSetupUrl { get; set; }
    public string? IssuedEmail { get; set; }
}
