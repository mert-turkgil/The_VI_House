using VIHouse.Entities.Referrals;

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
    DateTimeOffset StartAtUtc, DateTimeOffset EndAtUtc, long AmountMinor, string Currency)
{
    public string? Venue { get; init; }
    public bool IsOnline { get; init; }
    public string? TimeZoneId { get; init; }
    public string? TicketUrl { get; init; }
    public string? ExperienceUrl { get; init; }

    /// <summary>The experience as an .ics file, in the recipient's language.</summary>
    public string? CalendarUrl { get; init; }

    /// <summary>True when this is the buyer's first way into their account: a separate email with the
    /// password-setup link went out alongside this one, and the ticket page needs that password.</summary>
    public bool AccountSetupPending { get; init; }
}

/// <summary>Something changed on the account's sign-in side, or someone signed in from somewhere
/// new. What happened and where from; the way to lock things down if it was not them.</summary>
public record SecurityAlertEmailModel(
    string FirstName, string Event, string Detail, DateTimeOffset WhenUtc,
    string? IpAddress, string? UserAgent, string ActionUrl, string ActionLabel)
{
    /// <summary>Which alert this is — "PasswordChanged", "TwoFactorOn", "TwoFactorOff",
    /// "AuthenticatorReset", "NewSignIn" — so the template can word it in the reader's language.
    /// Event/Detail/ActionLabel are the English fallback.</summary>
    public string? Kind { get; init; }
}

public record PaymentFailedEmailModel(string FirstName, string ExperienceTitle, string InvitationUrl);

/// <summary>Internal notification sent to Site:ContactEmail when a visitor submits the public Contact page form.</summary>
public record ContactMessageEmailModel(string Name, string Email, string? Subject, string Message);

public enum MembershipEmailStatus { Confirmed, Renewed, Granted }

/// <summary>The membership status email — a new membership, a renewal, or one granted by an admin.
/// The positional members are what every send has; the rest fill the details card when known.</summary>
public record MembershipConfirmedEmailModel(string FirstName, string PlanName, DateTimeOffset? ExpiresAt)
{
    public MembershipEmailStatus Status { get; init; } = MembershipEmailStatus.Confirmed;
    public long? AmountMinor { get; init; }
    public string? Currency { get; init; }
    public string? MemberNumber { get; init; }
    public string? AccountUrl { get; init; }
}

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
public record WelcomeSetupEmailModel(string FirstName, string SetupUrl, string? PlanName)
{
    /// <summary>Set when the account was opened by an experience booking, so the mail can say what
    /// it is for ("your booking VI-26-1") instead of arriving out of nowhere.</summary>
    public string? BookingReference { get; init; }
    public string? ExperienceTitle { get; init; }

    /// <summary>How long the link works — the password-reset token lifespan.</summary>
    public int? ValidForHours { get; init; }

    /// <summary>True when an admin sent it by hand from the user record rather than a payment.</summary>
    public bool SentByAdmin { get; init; }
}

/// <summary>To an influencer when their link converts. Deliberately anonymous — what happened
/// (<paramref name="Kind"/>, worded by the template) and what it is worth, never who.</summary>
public record ReferralConvertedEmailModel(string Name, ReferralConversionKind Kind, string? Amount, string? Commission, string DashboardUrl);

/// <summary>The invitation to become an influencer. The link is the only way in, so it is only
/// ever in this email — the admin who sent it never sees it.</summary>
public record AmbassadorInviteEmailModel(string Name, string InviteUrl, string Code, decimal CommissionPercent, DateTimeOffset ExpiresAt);

/// <summary>An admin sends the influencer their referral link (and code) — the same thing their
/// area shows, delivered so it can be forwarded from the inbox.</summary>
public record AmbassadorLinkEmailModel(string Name, string ReferralUrl, string Code, decimal CommissionPercent, string DashboardUrl, string? Note);

/// <summary>To Finance and SuperAdmins: an influencer asked to be paid what they are owed.</summary>
public record WithdrawalRequestedEmailModel(string InfluencerName, string Amount, string? Note, string AdminUrl);

/// <summary>To the influencer: the House has sent the transfer.</summary>
public record WithdrawalPaidEmailModel(string Name, string Amount, string? Reference, string DashboardUrl);

/// <summary>To the influencer: their withdrawal request was declined, and why.</summary>
public record WithdrawalRejectedEmailModel(string Name, string Amount, string Reason, string DashboardUrl);

/// <summary>To the content staff: an influencer sent an article for review.</summary>
public record JournalSubmittedEmailModel(string InfluencerName, string Title, string AdminUrl);

/// <summary>To the influencer: the editors sent their article back with a note.</summary>
public record JournalChangesRequestedEmailModel(string Name, string Title, string Note, string WriterUrl);

/// <summary>To the influencer: their article is live.</summary>
public record JournalPublishedEmailModel(string Name, string Title, string PostUrl);

/// <summary>Sent when an existing SuperAdmin creates a staff account. Carries the one-time link the
/// new admin uses to set a password; no credential is ever emailed, and the account cannot be used
/// until they also confirm the address and switch on two-factor.</summary>
public record AdminInviteEmailModel(string FirstName, string SetupUrl, string InvitedBy, string RoleSummary);

/// <summary>"Forgot your password?" — the one-time reset link. Sent through the same pipeline as
/// every other mail (logged, templated); the Identity UI's own sender is a no-op and is never used.</summary>
public record PasswordResetEmailModel(string FirstName, string ResetUrl, int ExpiresInHours);

/// <summary>Sent to the <em>new</em> address when a member changes their email, so the change only
/// takes effect once whoever owns that inbox confirms it.</summary>
public record EmailChangeEmailModel(string FirstName, string NewEmail, string ConfirmUrl);

/// <summary>The subscription has ended (cancelled and now past its paid period, or revoked by the
/// House). Tells the member plainly what they lose and how to come back.</summary>
public record MembershipEndedEmailModel(string FirstName, string PlanName, DateTimeOffset EndedAt, string RejoinUrl, bool WasRevoked);

/// <summary>A decision, delivered gently. Reason is only present when the admin typed one meant for
/// the applicant; otherwise the mail says no more than that this round did not work out.</summary>
public record ApplicationRejectedEmailModel(string FirstName, string ExperienceTitle, string ExperienceCity, string? Reason, string ExperiencesUrl);

/// <summary>An update from the House to everyone confirmed on an experience — a schedule change, a
/// venue detail, a reminder — paired with the in-app notification of the same text.</summary>
public record ExperienceUpdateEmailModel(string FirstName, string ExperienceTitle, string Headline, string Body, string ExperienceUrl);

/// <summary>Sent the moment a seminar enrolment is confirmed — free, membership-covered or paid.
/// StartAtUtc is null for on-demand content, which has nothing to turn up to.</summary>
public record SeminarEnrolledEmailModel(
    string FirstName, string SeminarTitle, DateTimeOffset? StartAtUtc, bool IsOnline, string? Location, string SeminarUrl)
{
    /// <summary>The sitting as an .ics file; null for an on-demand session.</summary>
    public string? CalendarUrl { get; init; }
}

/// <summary>A checkout finished with a delayed payment method (bank transfer, direct debit): the
/// buyer did everything, the money is on its way, and nothing is granted until it lands.</summary>
public record PaymentProcessingEmailModel(string FirstName, string What, long AmountMinor, string Currency, string StatusUrl);

/// <summary>Money went back to the buyer, in full or in part. <paramref name="Effect"/> says what
/// that meant for the thing they bought — a cancelled booking, a released seat, nothing yet.</summary>
public record PaymentRefundedEmailModel(string FirstName, string What, long AmountRefundedMinor, string Currency, bool IsPartial, string Effect);

/// <summary>A message from the House to everyone on the launch list (the coming-soon sign-ups). The
/// admin writes Subject/Headline/Message once; the email's own chrome — footer, footnote, the leave
/// link — follows each recipient's language. There is no name to greet: the list holds an address and
/// nothing more, by design.</summary>
public record LaunchAnnouncementEmailModel(string Headline, string Message, string? ButtonLabel, string ButtonUrl, string LeaveUrl);

/// <summary>An admin changed which roles this account holds. Lists what was added and removed in
/// plain words, so a sudden new menu (or a missing one) is never a surprise.</summary>
public record RoleChangedEmailModel(string FirstName, IReadOnlyList<string> Added, IReadOnlyList<string> Removed, string AccountUrl);

/// <summary>Welcome to the founders: sent once, the moment the Founder role is granted.</summary>
public record FounderWelcomeEmailModel(string FirstName, string AccountUrl, string BenefitsUrl);

/// <summary>A plain test message from Admin › Emails, proving the SMTP settings deliver.</summary>
public record TestEmailModel(string SentBy, DateTimeOffset SentAtUtc, string Host);
