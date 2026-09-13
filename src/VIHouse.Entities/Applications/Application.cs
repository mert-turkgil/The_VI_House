using VIHouse.Entities.Common;

namespace VIHouse.Entities.Applications;

/// <summary>
/// The application-first funnel entry point (brief §25-28): Visitor -> Request Access ->
/// Application -> VI House Review -> Approved -> Private Payment Link -> Payment -> Member/Guest
/// account activated. UserId is null until an account is provisioned post-approval.
///
/// The applicant's answers are the same set the account profile holds (see
/// <see cref="Users.Profile"/>), so an approved application can seed the profile without asking
/// the same questions twice. Keep the two in step.
/// </summary>
public class Application : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid ExperienceId { get; set; }

    // Personal
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;

    /// <summary>Not collected by the public form any more. Kept nullable so an admin-entered or
    /// historic number still reaches the SMS path (see ApplicationService.SendInvitationAsync).</summary>
    public string? Phone { get; set; }

    /// <summary>Title / profession.</summary>
    public string? JobTitle { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string Country { get; set; } = default!;

    // Qualification
    /// <summary>"Describe yourself or your business." Required on the form.</summary>
    public string? AboutStatement { get; set; }

    /// <summary>"How may we help you with your goals, or what are your expectations?" Required on the form.</summary>
    public string? ExpectationsStatement { get; set; }

    /// <summary>One of <see cref="Users.EarningsBand"/>'s codes.</summary>
    public string? EarningsBand { get; set; }

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
