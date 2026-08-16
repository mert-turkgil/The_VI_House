using System.Security.Cryptography;
using System.Text.Json;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Communication;
using VIHouse.Entities.Compliance;

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
    IEmailLogRepository emailLogs,
    IAuditLogRepository auditLogs,
    IRepository<ConsentRecord> consentRecords,
    IRepository<ApplicationTag> tags) : IApplicationService
{
    private const string InvitationAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity

    private static readonly Dictionary<ApplicationStatus, ApplicationStatus[]> Allowed = new()
    {
        [ApplicationStatus.Submitted] = [ApplicationStatus.UnderReview],
        [ApplicationStatus.UnderReview] = [ApplicationStatus.Shortlisted],
        [ApplicationStatus.Shortlisted] = [ApplicationStatus.Approved, ApplicationStatus.Rejected, ApplicationStatus.Waitlisted],
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

    public async Task ApproveAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
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

        await emailLogs.AddAsync(new EmailLog
        {
            TemplateKey = "application-approved",
            RecipientEmail = application.Email,
            Subject = "You're approved — complete your booking",
            Status = EmailStatus.Queued, // no SMTP pipeline wired yet (brief plan milestone 8)
            RelatedEntityType = nameof(Application),
            RelatedEntityId = application.Id,
        }, ct);

        await applications.SaveChangesAsync(ct);
    }

    public Task RejectAsync(Guid id, Guid adminUserId, string? reason, string? ipAddress, CancellationToken ct = default) =>
        TransitionAsync(id, ApplicationStatus.Rejected, adminUserId, "ApplicationRejected", ipAddress, reason, ct: ct);

    public Task WaitlistAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default) =>
        TransitionAsync(id, ApplicationStatus.Waitlisted, adminUserId, "ApplicationWaitlisted", ipAddress, ct: ct);

    public async Task UpdateInternalNotesAsync(Guid id, string? notes, CancellationToken ct = default)
    {
        var application = await applications.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException($"Application {id} not found.");

        application.InternalNotes = notes;
        application.UpdatedAt = DateTimeOffset.UtcNow;
        await applications.SaveChangesAsync(ct);
    }

    public async Task AddTagAsync(Guid applicationId, string label, CancellationToken ct = default)
    {
        if (await applications.GetByIdAsync(applicationId, ct) is null)
            throw new InvalidOperationException($"Application {applicationId} not found.");

        // Added via the generic repo's own AddAsync (DbSet.Add), not by pushing into a loaded
        // Application's Tags collection navigation — same reasoning as ExperienceService's
        // ticket-type/inclusion/FAQ adds: avoids EF Core misreading a client-generated-GUID
        // entity's Added state as an Update.
        await tags.AddAsync(new ApplicationTag { ApplicationId = applicationId, Label = label }, ct);
        await tags.SaveChangesAsync(ct);
    }

    public async Task RemoveTagAsync(Guid applicationId, Guid tagId, CancellationToken ct = default)
    {
        var tag = await tags.GetByIdAsync(tagId, ct);
        if (tag is null || tag.ApplicationId != applicationId) return;

        tags.Remove(tag);
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
        application.ReviewedByUserId = adminUserId;
        application.ReviewedAt ??= DateTimeOffset.UtcNow;
        application.UpdatedAt = DateTimeOffset.UtcNow;

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
