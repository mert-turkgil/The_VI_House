using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Compliance;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Notifications;

namespace VIHouse.Business.Concrete;

/// <summary>
/// The application-first funnel state machine (brief §25-28). Transitions are whitelisted
/// explicitly in <see cref="Allowed"/> rather than left as a free-form status enum — an admin
/// action that tries an illegal jump (e.g. rejecting a Submitted application without review) throws
/// rather than silently corrupting the funnel's audit trail.
/// </summary>
public class ApplicationService(
    IApplicationRepository applications,
    IInvitationRepository invitations,
    IExperienceRepository experiences,
    IAuditLogRepository auditLogs,
    IRepository<ConsentRecord> consentRecords,
    IRepository<ApplicationTag> tags,
    IEmailService emailService,
    ISmsService smsService,
    INotificationService notificationService,
    IOptions<SiteOptions> siteOptions) : IApplicationService
{
    private const string InvitationAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity

    /// <summary>Audit-log actor id for transitions the Stripe webhook/checkout flow triggers — there's no admin behind these.</summary>
    private static readonly Guid SystemActorId = Guid.Empty;

    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Submitted] = [ApplicationStatus.UnderReview],
        [ApplicationStatus.UnderReview] = [ApplicationStatus.Shortlisted],
        [ApplicationStatus.Shortlisted] = [ApplicationStatus.Approved, ApplicationStatus.Rejected, ApplicationStatus.Waitlisted],

        // The waitlist is a holding pattern, not a verdict — it exists precisely because the room was
        // full, and the answer changes when a seat opens or the next cohort is announced. So it stays
        // decidable: approve or reject, as many times as it takes. (Waitlisting an already-waitlisted
        // application is left out on purpose — it would send a second "you're on the waitlist" email
        // saying nothing new.)
        [ApplicationStatus.Waitlisted] = [ApplicationStatus.Approved, ApplicationStatus.Rejected],

        [ApplicationStatus.Approved] = [ApplicationStatus.PaymentPending],
        [ApplicationStatus.PaymentPending] = [ApplicationStatus.Paid, ApplicationStatus.Approved],
    };

    public async Task<Application> SubmitAsync(Application application, bool agreedToTerms, string? ipAddress, CancellationToken ct = default)
    {
        application.Status = ApplicationStatus.Submitted;
        application.SubmittedAt = DateTimeOffset.UtcNow;

        await applications.AddAsync(application, ct);

        if (agreedToTerms)
        {
            await consentRecords.AddAsync(new ConsentRecord
            {
                ApplicationId = application.Id,
                Type = ConsentType.TermsOfService,
                Granted = true,
                Text = "I agree to The VI House Terms & Conditions and Privacy Policy.",
                GrantedAt = DateTimeOffset.UtcNow,
                IpAddress = ipAddress,
            }, ct);
        }

        await applications.SaveChangesAsync(ct);

        var experience = await experiences.GetByIdAsync(application.ExperienceId, ct);
        if (experience is not null)
        {
            await emailService.SendAsync(
                "ApplicationReceived", application.Email, "Application Received",
                new ApplicationReceivedEmailModel(application.FirstName, experience.Title, experience.City),
                nameof(Application), application.Id, ct);
        }

        return application;
    }

    public Task<List<Application>> GetByStatusAsync(ApplicationStatus? status, CancellationToken ct = default) =>
        status is null ? applications.GetAllAsync(ct) : applications.GetByStatusAsync(status.Value, ct);

    public Task<Application?> GetForAdminAsync(Guid id, CancellationToken ct = default) =>
        applications.GetWithTagsAsync(id, ct);

    public Task MarkUnderReviewAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default) =>
        TransitionAsync(id, ApplicationStatus.UnderReview, adminUserId, "ApplicationMarkedUnderReview", ipAddress, ct: ct);

    public Task ShortlistAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default) =>
        TransitionAsync(id, ApplicationStatus.Shortlisted, adminUserId, "ApplicationShortlisted", ipAddress, ct: ct);

    public async Task<InvitationDeliveryResult> ApproveAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var application = await TransitionAsync(id, ApplicationStatus.Approved, adminUserId, "ApplicationApproved", ipAddress, save: false, ct: ct);

        var invitation = new Invitation
        {
            Code = GenerateInvitationCode(),
            ApplicationId = application.Id,
            UserEmail = application.Email,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
        };
        await invitations.AddAsync(invitation, ct);
        await applications.SaveChangesAsync(ct);

        var experience = await experiences.GetByIdAsync(application.ExperienceId, ct);
        if (experience is null)
            return new InvitationDeliveryResult(false, "The experience behind this application no longer exists, so no payment link was sent.");

        return await SendInvitationAsync(application, experience, invitation, ct);
    }

    public async Task<InvitationDeliveryResult> ResendInvitationAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var application = await applications.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Application {id} not found.");

        if (application.Status is not (ApplicationStatus.Approved or ApplicationStatus.PaymentPending))
            return new InvitationDeliveryResult(false, "Only an approved application has a payment link to send.");

        var experience = await experiences.GetByIdAsync(application.ExperienceId, ct);
        if (experience is null)
            return new InvitationDeliveryResult(false, "The experience behind this application no longer exists.");

        var invitation = await invitations.GetLatestByApplicationAsync(id, ct);

        if (invitation is { IsUsed: true })
            return new InvitationDeliveryResult(false, "That invitation has already been used — they have paid, so there is nothing left to send.");

        // An expired link cannot be un-expired, and resending one only walks them into the "this
        // invitation has expired" page. Issue a fresh code instead, on the same 14-day window.
        var reissued = invitation is null || invitation.ExpiresAt < DateTimeOffset.UtcNow;
        if (reissued)
        {
            invitation = new Invitation
            {
                Code = GenerateInvitationCode(),
                ApplicationId = application.Id,
                UserEmail = application.Email,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(14),
            };
            await invitations.AddAsync(invitation, ct);
        }

        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = reissued ? "InvitationReissued" : "InvitationResent",
            EntityType = nameof(Application),
            EntityId = application.Id,
            IpAddress = ipAddress,
        }, ct);
        await applications.SaveChangesAsync(ct);

        var result = await SendInvitationAsync(application, experience, invitation!, ct);
        return reissued
            ? result with { Message = "The old link had expired, so a new one was issued. " + result.Message }
            : result;
    }

    /// <summary>
    /// The private payment link, out over both channels the applicant gave us.
    ///
    /// Two channels for one link is deliberate rather than belt-and-braces: this is the only door into
    /// the booking, it expires in 14 days, and an approval that lands in a spam folder is an empty
    /// seat nobody finds out about until the room is smaller than it should have been.
    /// </summary>
    private async Task<InvitationDeliveryResult> SendInvitationAsync(
        Application application, Experience experience, Invitation invitation, CancellationToken ct)
    {
        var invitationUrl = $"{siteOptions.Value.BaseUrl.TrimEnd('/')}/invitation/{invitation.Code}";

        var emailed = await emailService.SendAsync(
            "ApplicationApproved", application.Email, "You're approved — complete your booking",
            new ApplicationApprovedEmailModel(application.FirstName, experience.Title, experience.City, invitationUrl, invitation.ExpiresAt),
            nameof(Application), application.Id, ct);

        var texted = await smsService.SendAsync(
            "ApplicationApproved", application.Phone,
            $"The VI House: you're approved for {experience.City}. Complete your booking: {invitationUrl} "
                + $"— the link expires {invitation.ExpiresAt:d MMM}.",
            nameof(Application), application.Id, ct);

        // No-ops silently if this applicant has no account yet (the common case — accounts are
        // only provisioned at checkout). Only ever fires for an existing member re-applying.
        await notificationService.CreateForEmailAsync(
            application.Email, NotificationType.ApplicationApproved,
            "Application Approved", $"Your application for The VI House — {experience.City} was approved.",
            invitationUrl, ct);

        return new InvitationDeliveryResult(emailed || texted, DescribeDelivery(emailed, texted, application.Phone));
    }

    /// <summary>
    /// What to tell the admin who just pressed the button. The distinction that matters is between a
    /// message that failed and one that was never attempted — "no SMS gateway is configured" is a
    /// setup task, "the gateway refused it" is a phone number problem, and both look identical if
    /// they're reported as "not sent".
    /// </summary>
    private string DescribeDelivery(bool emailed, bool texted, string? phone) => (emailed, texted) switch
    {
        (true, true) => "The payment link went out by email and text message.",
        (true, false) when !smsService.IsConfigured => "The payment link was emailed. No SMS gateway is configured, so nothing was texted.",
        (true, false) when string.IsNullOrWhiteSpace(phone) => "The payment link was emailed. This application has no phone number on it.",
        (true, false) => "The payment link was emailed, but the text message didn't go out — Emails & SMS has the reason.",
        (false, true) => "The payment link went out by text message, but the email failed — Emails & SMS has the reason.",
        _ => "Neither the email nor the text message went out — Emails & SMS has the reason.",
    };

    public Task RejectAsync(Guid id, Guid adminUserId, string? reason, string? ipAddress, CancellationToken ct = default) =>
        TransitionAsync(id, ApplicationStatus.Rejected, adminUserId, "ApplicationRejected", ipAddress, reason, ct: ct);

    public async Task WaitlistAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var application = await TransitionAsync(id, ApplicationStatus.Waitlisted, adminUserId, "ApplicationWaitlisted", ipAddress, ct: ct);

        var experience = await experiences.GetByIdAsync(application.ExperienceId, ct);
        if (experience is not null)
        {
            await emailService.SendAsync(
                "ApplicationWaitlisted", application.Email, "You're on the waitlist",
                new ApplicationWaitlistedEmailModel(application.FirstName, experience.Title),
                nameof(Application), application.Id, ct);
        }
    }

    public Task MarkPaymentPendingAsync(Guid id, CancellationToken ct = default) =>
        TransitionAsync(id, ApplicationStatus.PaymentPending, SystemActorId, "CheckoutStarted", null, ct: ct);

    public Task MarkPaidAsync(Guid id, CancellationToken ct = default) =>
        TransitionAsync(id, ApplicationStatus.Paid, SystemActorId, "PaymentConfirmed", null, ct: ct);

    public Task RevertToApprovedAsync(Guid id, CancellationToken ct = default) =>
        TransitionAsync(id, ApplicationStatus.Approved, SystemActorId, "CheckoutAbandoned", null, ct: ct);

    public async Task UpdateInternalNotesAsync(Guid id, string? notes, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var application = await applications.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Application {id} not found.");

        application.InternalNotes = notes;
        application.UpdatedAt = DateTimeOffset.UtcNow;

        // Notes can contain sensitive assessment text — log that a change happened, not the
        // content itself, consistent with AuditLogEntry never carrying free-text PII.
        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = "ApplicationNotesUpdated",
            EntityType = nameof(Application),
            EntityId = id,
            IpAddress = ipAddress,
        }, ct);

        await applications.SaveChangesAsync(ct);
    }

    public async Task AddTagAsync(Guid applicationId, string label, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await applications.GetByIdAsync(applicationId, ct) is null)
            throw new InvalidOperationException($"Application {applicationId} not found.");

        // Added via the generic repo's own AddAsync (DbSet.Add), not by pushing into a loaded
        // Application's Tags collection navigation — same reasoning as ExperienceService's
        // ticket-type/inclusion/FAQ adds: avoids EF Core misreading a client-generated-GUID
        // entity's Added state as an Update.
        var tag = new ApplicationTag { ApplicationId = applicationId, Label = label };
        await tags.AddAsync(tag, ct);
        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = "ApplicationTagAdded",
            EntityType = nameof(ApplicationTag),
            EntityId = tag.Id,
            DataAfter = JsonSerializer.Serialize(new { ApplicationId = applicationId, Label = label }),
            IpAddress = ipAddress,
        }, ct);
        await tags.SaveChangesAsync(ct);
    }

    public async Task RemoveTagAsync(Guid applicationId, Guid tagId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var tag = await tags.GetByIdAsync(tagId, ct);
        if (tag is null || tag.ApplicationId != applicationId) return;

        tags.Remove(tag);
        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = "ApplicationTagRemoved",
            EntityType = nameof(ApplicationTag),
            EntityId = tagId,
            DataBefore = JsonSerializer.Serialize(new { ApplicationId = applicationId, tag.Label }),
            IpAddress = ipAddress,
        }, ct);
        await tags.SaveChangesAsync(ct);
    }

    private async Task<Application> TransitionAsync(
        Guid id,
        ApplicationStatus target,
        Guid adminUserId,
        string action,
        string? ipAddress,
        string? reason = null,
        bool save = true,
        CancellationToken ct = default)
    {
        var application = await applications.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Application {id} not found.");

        if (!Allowed.TryGetValue(application.Status, out var next) || !next.Contains(target))
            throw new InvalidOperationException($"Cannot transition Application {id} from {application.Status} to {target}.");

        var before = application.Status;
        application.Status = target;
        application.UpdatedAt = DateTimeOffset.UtcNow;

        // System-triggered transitions (Stripe checkout flow) aren't a "review" — leave the
        // original reviewing admin's attribution on the record instead of overwriting it.
        if (adminUserId != SystemActorId)
        {
            application.ReviewedByUserId = adminUserId;
            application.ReviewedAt ??= DateTimeOffset.UtcNow;
        }

        if (target is ApplicationStatus.Approved or ApplicationStatus.Rejected or ApplicationStatus.Waitlisted)
        {
            application.DecisionAt = DateTimeOffset.UtcNow;
            if (reason is not null) application.DecisionReason = reason;
        }

        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(Application),
            EntityId = application.Id,
            DataBefore = JsonSerializer.Serialize(new { Status = before.ToString() }),
            DataAfter = JsonSerializer.Serialize(new { Status = target.ToString(), Reason = reason }),
            IpAddress = ipAddress,
        }, ct);

        if (save)
            await applications.SaveChangesAsync(ct);

        return application;
    }

    private static string GenerateInvitationCode() =>
        RandomNumberGenerator.GetString(InvitationAlphabet, 10);
}
