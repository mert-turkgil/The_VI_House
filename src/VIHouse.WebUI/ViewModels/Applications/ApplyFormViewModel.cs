using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.ViewModels.Applications;

/// <summary>
/// The application form. The questions are the House's one set — the same ones /join asks and the
/// account profile holds (see ProfileFormViewModel) — so an approved applicant's answers become
/// their profile without being asked again. Two statements are required; everything else about
/// the person is optional, and nothing about their company beyond what fits in a title.
/// </summary>
public class ApplyFormViewModel
{
    public Guid ExperienceId { get; set; }
    public string ExperienceSlug { get; set; } = default!;
    public string ExperienceTitle { get; set; } = default!;
    public string ExperienceCity { get; set; } = default!;
    public string ExperienceCountry { get; set; } = default!;
    public DateTimeOffset ExperienceStartAtUtc { get; set; }
    public DateTimeOffset ExperienceEndAtUtc { get; set; }

    // Not posted back (no hidden <input> for these — see Index.cshtml): the hero photo is the
    // experience's own, re-read from the database on every GET and on every failed-POST
    // redisplay via RefreshExperienceFields, the same as every other Experience* field above. A
    // hidden field would let a visitor swap in an arbitrary image URL for their own request.
    public string? ExperienceCoverImageUrl { get; set; }
    public string? ExperienceCoverImageAlt { get; set; }

    // Personal
    [Required, StringLength(100)]
    public string FirstName { get; set; } = default!;

    [Required, StringLength(100)]
    public string LastName { get; set; } = default!;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = default!;

    [StringLength(200)]
    public string? JobTitle { get; set; }

    // Address
    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    // No [StringLength] here on purpose: the real check is Countries.IsValid in the controller,
    // run after Countries.Normalize has a chance to recover a browser-autofilled country name
    // (e.g. "Cyprus") back into its code ("CY") — see Countries.Normalize's own comment. A length
    // attribute here would reject that value before the controller ever saw it.
    [Required(ErrorMessage = "Choose your country.")]
    public string Country { get; set; } = default!;

    // About
    [Required(ErrorMessage = "Tell us a little about yourself or your business.")]
    [StringLength(2000)]
    public string AboutStatement { get; set; } = default!;

    [Required(ErrorMessage = "Tell us what you're hoping for.")]
    [StringLength(2000)]
    public string ExpectationsStatement { get; set; } = default!;

    [StringLength(20)]
    public string? EarningsBand { get; set; }

    [StringLength(40)]
    public string? ReferralCode { get; set; }

    // Deliberately not a [Range(typeof(bool),...)] attribute: jQuery Validate's unobtrusive "range"
    // adapter does a numeric comparison (parseFloat) against the "true"/"false" strings that
    // produces, which is always NaN — the field would fail client-side validation even when
    // checked. Enforced explicitly server-side instead (see ApplicationController).
    public bool AgreeToTerms { get; set; }
}
