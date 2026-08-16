using VIHouse.Entities.Common;

namespace VIHouse.Entities.Applications;

/// <summary>
/// The application-first funnel entry point (brief §25-28): Visitor -> Request Access ->
/// Application -> VI House Review -> Approved -> Private Payment Link -> Payment -> Member/Guest
/// account activated. UserId is null until an account is provisioned post-approval.
/// </summary>
public class Application : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid ExperienceId { get; set; }

    // Personal
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string Country { get; set; } = default!;
    public string? City { get; set; }

    // Professional
    public string? CompanyName { get; set; }
    public string? JobTitle { get; set; }
    public string? Industry { get; set; }
    public string? CompanyStage { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? WebsiteUrl { get; set; }

    // Qualification
    public int? YearsOfExperience { get; set; }
    public string? AnnualRevenueBand { get; set; }
    public string? FundingStage { get; set; }
    public string? MotivationStatement { get; set; }      // "Why do you want to join The VI House?"
    public string? BuildingStatement { get; set; }         // "What are you building?"
    public string? ContributionStatement { get; set; }     // "What could you bring to the community?"
    public string? HowDidYouHear { get; set; }
    public string? ReferralCode { get; set; }

    /// <summary>Admin-settable qualification score, not customer-visible.</summary>
    public int? QualificationScore { get; set; }

    // Workflow
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    /// <summary>Admin-only notes. Must never be rendered on any public/member-facing view.</summary>
    public string? InternalNotes { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? DecisionAt { get; set; }
    public string? DecisionReason { get; set; }

    public List<ApplicationTag> Tags { get; set; } = [];
}
