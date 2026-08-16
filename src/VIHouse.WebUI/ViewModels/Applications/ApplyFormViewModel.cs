using System.ComponentModel.DataAnnotations;

namespace VIHouse.WebUI.ViewModels.Applications;

public class ApplyFormViewModel
{
    public Guid ExperienceId { get; set; }
    public string ExperienceSlug { get; set; } = default!;
    public string ExperienceTitle { get; set; } = default!;
    public string ExperienceCity { get; set; } = default!;
    public string ExperienceCountry { get; set; } = default!;
    public DateTimeOffset ExperienceStartAtUtc { get; set; }
    public DateTimeOffset ExperienceEndAtUtc { get; set; }

    // Personal
    [Required, StringLength(100)]
    public string FirstName { get; set; } = default!;

    [Required, StringLength(100)]
    public string LastName { get; set; } = default!;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = default!;

    [Phone, StringLength(32)]
    public string? Phone { get; set; }

    [Required, StringLength(2, MinimumLength = 2, ErrorMessage = "Use a 2-letter country code.")]
    public string Country { get; set; } = default!;

    [StringLength(100)]
    public string? City { get; set; }

    // Professional
    [StringLength(150)]
    public string? CompanyName { get; set; }

    [StringLength(150)]
    public string? JobTitle { get; set; }

    [StringLength(100)]
    public string? Industry { get; set; }

    [StringLength(50)]
    public string? CompanyStage { get; set; }

    [Url, StringLength(300)]
    public string? LinkedInUrl { get; set; }

    [Url, StringLength(300)]
    public string? WebsiteUrl { get; set; }

    // Qualification
    [Range(0, 60)]
    public int? YearsOfExperience { get; set; }

    [StringLength(50)]
    public string? AnnualRevenueBand { get; set; }

    [StringLength(50)]
    public string? FundingStage { get; set; }

    [Required, StringLength(2000)]
    public string MotivationStatement { get; set; } = default!;

    [Required, StringLength(2000)]
    public string BuildingStatement { get; set; } = default!;

    [StringLength(2000)]
    public string? ContributionStatement { get; set; }

    [StringLength(100)]
    public string? HowDidYouHear { get; set; }

    [StringLength(40)]
    public string? ReferralCode { get; set; }

    // Deliberately not a [Range(typeof(bool),...)] attribute: jQuery Validate's unobtrusive "range"
    // adapter does a numeric comparison (parseFloat) against the "true"/"false" strings that
    // produces, which is always NaN — the field would fail client-side validation even when
    // checked. Enforced explicitly server-side instead (see ApplicationController).
    public bool AgreeToTerms { get; set; }
}
