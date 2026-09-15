namespace VIHouse.Business.Abstract;

// Plain content DTOs for the transactional email templates (brief §69). These live in Business
// (not WebUI) because ApplicationService/PaymentService construct them, and Business can't
// reference WebUI — the Razor views under WebUI/Views/Emails/ reference these types instead.

public record ApplicationReceivedEmailModel(string FirstName, string ExperienceTitle, string ExperienceCity);

public record ApplicationApprovedEmailModel(string FirstName, string ExperienceTitle, string ExperienceCity, string InvitationUrl, DateTimeOffset ExpiresAt);

public record ApplicationWaitlistedEmailModel(string FirstName, string ExperienceTitle);

/// <summary>
/// Sent when someone puts themselves on the public waitlist for a full experience — distinct from
/// <see cref="ApplicationWaitlistedEmailModel"/>, which is an admin moving an existing application
/// sideways. This one goes to people who may have no account at all, so it carries the position:
/// "you're 7th" is the only thing they have to hold on to.
/// </summary>
public record ExperienceWaitlistEmailModel(string FirstName, string ExperienceTitle, string ExperienceCity, int Position);

public record BookingConfirmedEmailModel(
    string FirstName, string BookingReference, string ExperienceTitle, string ExperienceCity,
    DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc, long AmountMinor, string Currency);

public record PaymentFailedEmailModel(string FirstName, string ExperienceTitle, string InvitationUrl);

/// <summary>Internal notification sent to Site:ContactEmail when a visitor submits the public Contact page form.</summary>
public record ContactMessageEmailModel(string Name, string Email, string? Subject, string Message);

public record MembershipConfirmedEmailModel(string FirstName, string PlanName, DateTimeOffset? ExpiresAt);

/// <summary>Sent when the provider bills a recurring membership for a further period.</summary>
public record MembershipRenewedEmailModel(string FirstName, string PlanName, DateTimeOffset ExpiresAt);

/// <summary>Sent once, when a membership checkout lapses unpaid — carries the resume link that
/// reopens checkout for the same form without filling it in again.</summary>
public record MembershipResumeEmailModel(string FirstName, string PlanName, string ResumeUrl);

/// <summary>Sent once, when a renewal charge first fails. ActionUrl is wherever the member can fix
/// it — the provider's hosted invoice when it has one, the billing portal otherwise. AccessUntil is
/// the end of the period already paid for; NextAttemptAt is the provider's next retry, if any.</summary>
public record MembershipPaymentFailedEmailModel(string FirstName, string PlanName, string ActionUrl, DateTimeOffset? AccessUntil, DateTimeOffset? NextAttemptAt);

/// <summary>Sent during onboarding to prove the member owns the address they signed up with.</summary>
public record ConfirmEmailAddressEmailModel(string FirstName, string ConfirmUrl);

/// <summary>Sent the moment an account is provisioned by a completed payment — carries the one-time
/// link the new member uses to choose a password and start onboarding. Nothing else in the system
/// ever emails a credential.</summary>
public record WelcomeSetupEmailModel(string FirstName, string SetupUrl, string? PlanName);

/// <summary>Sent when an existing SuperAdmin creates a staff account. Carries the one-time link the
/// new admin uses to set a password; no credential is ever emailed, and the account cannot be used
/// until they also confirm the address and switch on two-factor.</summary>
/// <summary>To an ambassador when their link converts. Deliberately anonymous — what happened and
/// what it is worth, never who.</summary>
public record ReferralConvertedEmailModel(string Name, string What, string? Amount, string? Commission, string DashboardUrl);

public record AdminInviteEmailModel(string FirstName, string SetupUrl, string InvitedBy, string RoleSummary);

/// <summary>Sent the moment a seminar enrolment is confirmed — free, membership-covered or paid.
/// StartAtUtc is null for on-demand content, which has nothing to turn up to.</summary>
public record SeminarEnrolledEmailModel(
    string FirstName, string SeminarTitle, DateTimeOffset? StartAtUtc, bool IsOnline, string? Location, string SeminarUrl);
