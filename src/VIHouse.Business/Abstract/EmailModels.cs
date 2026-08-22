namespace VIHouse.Business.Abstract;

// Plain content DTOs for the transactional email templates (brief §69). These live in Business
// (not WebUI) because ApplicationService/PaymentService construct them, and Business can't
// reference WebUI — the Razor views under WebUI/Views/Emails/ reference these types instead.

public record ApplicationReceivedEmailModel(string FirstName, string ExperienceTitle, string ExperienceCity);

public record ApplicationApprovedEmailModel(string FirstName, string ExperienceTitle, string ExperienceCity, string InvitationUrl, DateTimeOffset ExpiresAt);

public record ApplicationWaitlistedEmailModel(string FirstName, string ExperienceTitle);

public record BookingConfirmedEmailModel(
    string FirstName, string BookingReference, string ExperienceTitle, string ExperienceCity,
    DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc, long AmountMinor, string Currency);

public record PaymentFailedEmailModel(string FirstName, string ExperienceTitle, string InvitationUrl);

/// <summary>Internal notification sent to Site:ContactEmail when a visitor submits the public Contact page form.</summary>
public record ContactMessageEmailModel(string Name, string Email, string? Subject, string Message);

public record MembershipConfirmedEmailModel(string FirstName, string PlanName, DateTimeOffset? ExpiresAt);

/// <summary>Sent during onboarding to prove the member owns the address they signed up with.</summary>
public record ConfirmEmailAddressEmailModel(string FirstName, string ConfirmUrl);

/// <summary>Sent the moment an account is provisioned by a completed payment — carries the one-time
/// link the new member uses to choose a password and start onboarding. Nothing else in the system
/// ever emails a credential.</summary>
public record WelcomeSetupEmailModel(string FirstName, string SetupUrl, string? PlanName);

/// <summary>Sent when an existing SuperAdmin creates a staff account. Carries the one-time link the
/// new admin uses to set a password; no credential is ever emailed, and the account cannot be used
/// until they also confirm the address and switch on two-factor.</summary>
public record AdminInviteEmailModel(string FirstName, string SetupUrl, string InvitedBy, string RoleSummary);
