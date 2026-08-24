using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Notifications;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Concrete;

public class SeminarService(
    ISeminarRepository seminars,
    ISeminarEnrollmentRepository enrollments,
    // Generic repositories for the two child collections, used for inserts and deletes rather than
    // pushing into the loaded Seminar's navigation properties. EF Core cannot reliably tell a "new"
    // entity from an "existing" one by graph discovery once it already carries a non-default,
    // client-generated Guid key (which BaseEntity always sets) — it tracked new rows as Modified and
    // issued an UPDATE that matched nothing, surfacing as a bogus DbUpdateConcurrencyException.
    // DbSet.Add()/Remove() states them unambiguously. Same reasoning, and the same fix, as
    // ExperienceService.AddTicketTypeAsync.
    IRepository<SeminarTranslation> translations,
    IRepository<SeminarMedia> mediaRows,
    IMembershipService membershipService,
    IPaymentProvider paymentProvider,
    IMediaStorage mediaStorage,
    IEmailService emailService,
    INotificationService notificationService,
    IAuditLogRepository auditLogs,
    IOptions<SiteOptions> siteOptions,
    UserManager<ApplicationUser> userManager) : ISeminarService
{
    // --- Public reads --------------------------------------------------------------------------

    public Task<List<Seminar>> GetPublicListingAsync(SeminarFilter filter, CancellationToken ct = default) =>
        seminars.GetPublicListingAsync(filter, ct);

    public async Task<Seminar?> GetPublicDetailBySlugAsync(
        string slug, bool viewerIsMember, bool viewerIsStaff = false, CancellationToken ct = default)
    {
        var seminar = await seminars.GetBySlugAsync(slug, ct);
        if (seminar is null) return null;

        // Staff see the page exactly as it will look, at any status and any visibility — that is
        // what the panel's preview link is for. Everyone else goes through the two checks below.
        if (viewerIsStaff) return seminar;

        // Draft is invisible to everyone outside the admin panel; Archived stays readable so the
        // people who enrolled while it was live do not lose the recording they paid for.
        if (seminar.Status == SeminarStatus.Draft) return null;

        // Members-only means members-only for the *page*, not just the content. Returning null here
        // (which the controller turns into a 404) rather than challenging for a login keeps the
        // existence of a private session from being confirmed to anyone who guesses the slug.
        if (seminar.Visibility == SeminarVisibility.Members && !viewerIsMember) return null;

        return seminar;
    }

    public async Task<SeminarAccessInfo> GetAccessAsync(Seminar seminar, Guid? userId, CancellationToken ct = default)
    {
        var seatsRemaining = await SeatsRemainingAsync(seminar, ct);

        if (userId is null)
            return new SeminarAccessInfo(SeminarAccessOutcome.NeedsSignIn, seminar.PriceMinor, seminar.Currency, seatsRemaining);

        // An existing enrolment outranks everything below it, including a sold-out or archived
        // seminar: someone who already has a place keeps it when the last seat goes.
        var existing = await enrollments.GetForUserAsync(seminar.Id, userId.Value, ct);
        if (existing is { Status: SeminarEnrollmentStatus.Confirmed })
            return new SeminarAccessInfo(SeminarAccessOutcome.Enrolled, seminar.PriceMinor, seminar.Currency, seatsRemaining);

        if (existing is { Status: SeminarEnrollmentStatus.Pending })
            return new SeminarAccessInfo(SeminarAccessOutcome.PendingPayment, seminar.PriceMinor, seminar.Currency, seatsRemaining);

        if (seminar.Status != SeminarStatus.Published)
            return new SeminarAccessInfo(SeminarAccessOutcome.NotOpen, seminar.PriceMinor, seminar.Currency, seatsRemaining);

        if (seatsRemaining is 0)
            return new SeminarAccessInfo(SeminarAccessOutcome.SoldOut, seminar.PriceMinor, seminar.Currency, seatsRemaining);

        if (seminar.PriceMinor <= 0)
            return new SeminarAccessInfo(SeminarAccessOutcome.FreeToEnrol, 0, seminar.Currency, seatsRemaining);

        // The "free if you're subscribed" rule. Read live rather than from a claim, so a lapsed
        // membership stops covering seminars the moment it lapses rather than at next sign-in.
        if (seminar.IncludedWithMembership && await membershipService.GetCurrentMembershipAsync(userId.Value, ct) is not null)
            return new SeminarAccessInfo(SeminarAccessOutcome.IncludedInMembership, seminar.PriceMinor, seminar.Currency, seatsRemaining);

        return new SeminarAccessInfo(SeminarAccessOutcome.RequiresPayment, seminar.PriceMinor, seminar.Currency, seatsRemaining);
    }

    public async Task<List<Seminar>> GetEnrolledSeminarsAsync(Guid userId, CancellationToken ct = default)
    {
        var mine = await enrollments.GetConfirmedForUserAsync(userId, ct);
        if (mine.Count == 0) return [];

        var ids = mine.Select(e => e.SeminarId).Distinct().ToList();
        var found = await seminars.GetByIdsAsync(ids, ct);

        // Ordered by when they enrolled, not by the seminar's own sort order — this is a personal
        // list, and "the one I signed up for most recently" is what someone is looking for. The
        // enrolment list already arrives newest-first, so projecting through it preserves that.
        return [.. mine
            .Select(e => found.FirstOrDefault(s => s.Id == e.SeminarId))
            .OfType<Seminar>()];
    }

    // --- Enrolment -----------------------------------------------------------------------------

    public async Task<SeminarEnrollmentResult> EnrollAsync(Guid seminarId, Guid userId, CancellationToken ct = default)
    {
        // WithDetail rather than GetById: the confirmation email and notification both need the
        // seminar's title, which lives on its translations.
        var seminar = await seminars.GetWithDetailAsync(seminarId, ct);
        if (seminar is null) return SeminarEnrollmentResult.Fail("Seminar.Error.NotFound");

        // Re-derived here rather than trusted from the form: the page may have been rendered when
        // this was free, and the only place the free/paid split can safely be decided is the server.
        var access = await GetAccessAsync(seminar, userId, ct);

        var grant = access.Outcome switch
        {
            SeminarAccessOutcome.FreeToEnrol => SeminarAccessGrant.Free,
            SeminarAccessOutcome.IncludedInMembership => SeminarAccessGrant.Membership,
            _ => (SeminarAccessGrant?)null,
        };

        if (grant is null)
        {
            return SeminarEnrollmentResult.Fail(access.Outcome switch
            {
                SeminarAccessOutcome.Enrolled => "Seminar.Error.AlreadyEnrolled",
                SeminarAccessOutcome.PendingPayment => "Seminar.Error.PaymentPending",
                SeminarAccessOutcome.SoldOut => "Seminar.Error.SoldOut",
                SeminarAccessOutcome.NotOpen => "Seminar.Error.NotOpen",
                SeminarAccessOutcome.RequiresPayment => "Seminar.Error.PaymentRequired",
                _ => "Seminar.Error.SignInRequired",
            });
        }

        var enrollment = await UpsertEnrollmentAsync(seminarId, userId, ct);
        enrollment.Status = SeminarEnrollmentStatus.Confirmed;
        enrollment.GrantedVia = grant.Value;
        enrollment.AmountMinor = 0;
        enrollment.Currency = seminar.Currency;
        enrollment.ProviderReference = null;
        enrollment.ConfirmedAt = DateTimeOffset.UtcNow;
        enrollment.UpdatedAt = DateTimeOffset.UtcNow;

        await enrollments.SaveChangesAsync(ct);
        await AnnounceEnrolmentAsync(seminar, userId, ct);

        return SeminarEnrollmentResult.Enrolled();
    }

    public async Task<SeminarEnrollmentResult> InitiateCheckoutAsync(
        Guid seminarId, Guid userId, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var seminar = await seminars.GetWithDetailAsync(seminarId, ct);
        if (seminar is null) return SeminarEnrollmentResult.Fail("Seminar.Error.NotFound");

        var access = await GetAccessAsync(seminar, userId, ct);
        if (access.Outcome != SeminarAccessOutcome.RequiresPayment)
        {
            return SeminarEnrollmentResult.Fail(access.Outcome switch
            {
                SeminarAccessOutcome.Enrolled => "Seminar.Error.AlreadyEnrolled",
                SeminarAccessOutcome.PendingPayment => "Seminar.Error.PaymentPending",
                SeminarAccessOutcome.SoldOut => "Seminar.Error.SoldOut",
                SeminarAccessOutcome.NotOpen => "Seminar.Error.NotOpen",
                // Free and membership-covered both belong on the no-payment path — sending them to
                // a checkout would charge someone for something they are entitled to.
                _ => "Seminar.Error.NoPaymentNeeded",
            });
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return SeminarEnrollmentResult.Fail("Seminar.Error.AccountMissing");

        var enrollment = await UpsertEnrollmentAsync(seminarId, userId, ct);
        enrollment.Status = SeminarEnrollmentStatus.Pending;
        enrollment.GrantedVia = SeminarAccessGrant.Purchase;
        enrollment.AmountMinor = seminar.PriceMinor;
        enrollment.Currency = seminar.Currency;
        enrollment.ConfirmedAt = null;
        // Placeholder, unique — replaced the moment the provider hands back a session id. The row
        // exists before the browser is ever redirected, so a payment can never arrive with nothing
        // locally to attach it to (brief §32).
        enrollment.ProviderReference = $"pending_{enrollment.Id:N}";
        enrollment.UpdatedAt = DateTimeOffset.UtcNow;
        await enrollments.SaveChangesAsync(ct);

        var translation = SeminarContent.Resolve(seminar, SiteCultures.Default);

        try
        {
            var session = await paymentProvider.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest(
                CustomerEmail: user.Email!,
                ProductName: $"The VI House Session — {translation?.Title ?? seminar.Slug}",
                ProductDescription: translation?.Summary,
                AmountMinor: seminar.PriceMinor,
                Currency: seminar.Currency,
                SuccessUrl: successUrl,
                CancelUrl: cancelUrl,
                ClientReferenceId: enrollment.Id.ToString(),
                Metadata: new Dictionary<string, string>
                {
                    ["seminarEnrollmentId"] = enrollment.Id.ToString(),
                    ["seminarId"] = seminar.Id.ToString(),
                    ["userId"] = userId.ToString(),
                }), ct);

            enrollment.ProviderReference = session.SessionId;
            await enrollments.SaveChangesAsync(ct);

            return SeminarEnrollmentResult.Redirect(session.Url);
        }
        catch (Exception)
        {
            // The pending row is left in place deliberately: it is reused on the next attempt (see
            // UpsertEnrollmentAsync), and deleting it here would race a webhook for a session that
            // was in fact created before the response failed to reach us.
            return SeminarEnrollmentResult.Fail("Seminar.Error.ProviderUnreachable");
        }
    }

    public async Task<SeminarConfirmationInfo?> GetConfirmationBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        var enrollment = await enrollments.GetByProviderReferenceAsync(sessionId, ct);
        if (enrollment is null) return null;

        var seminar = await seminars.GetWithDetailAsync(enrollment.SeminarId, ct);
        var title = seminar is null ? null : SeminarContent.Title(seminar, SiteCultures.Default);

        return new SeminarConfirmationInfo(
            enrollment.Status == SeminarEnrollmentStatus.Confirmed,
            title, seminar?.Slug, enrollment.AmountMinor, enrollment.Currency);
    }

    public async Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        if (webhookEvent.SessionId is null) return;

        if (webhookEvent.Type == PaymentWebhookEventType.CheckoutCompleted)
            await HandleCheckoutCompletedAsync(webhookEvent.SessionId, ct);
        else if (webhookEvent.Type == PaymentWebhookEventType.CheckoutExpired)
            await HandleCheckoutExpiredAsync(webhookEvent.SessionId, ct);
    }

    private async Task HandleCheckoutCompletedAsync(string sessionId, CancellationToken ct)
    {
        var enrollment = await enrollments.GetByProviderReferenceAsync(sessionId, ct);

        // Unknown session (a ticket or membership purchase — see PaymentService/MembershipService),
        // or one already handled. Either way there is nothing to do, and the double delivery Stripe
        // is entitled to make costs nothing.
        if (enrollment is null || enrollment.Status == SeminarEnrollmentStatus.Confirmed) return;

        enrollment.Status = SeminarEnrollmentStatus.Confirmed;
        enrollment.GrantedVia = SeminarAccessGrant.Purchase;
        enrollment.ConfirmedAt = DateTimeOffset.UtcNow;
        enrollment.UpdatedAt = DateTimeOffset.UtcNow;
        await enrollments.SaveChangesAsync(ct);

        var seminar = await seminars.GetWithDetailAsync(enrollment.SeminarId, ct);
        if (seminar is not null)
            await AnnounceEnrolmentAsync(seminar, enrollment.UserId, ct);
    }

    private async Task HandleCheckoutExpiredAsync(string sessionId, CancellationToken ct)
    {
        var enrollment = await enrollments.GetByProviderReferenceAsync(sessionId, ct);
        if (enrollment is null || enrollment.Status == SeminarEnrollmentStatus.Confirmed) return;

        enrollment.Status = SeminarEnrollmentStatus.Cancelled;
        enrollment.UpdatedAt = DateTimeOffset.UtcNow;
        await enrollments.SaveChangesAsync(ct);
    }

    // --- Admin: seminars -----------------------------------------------------------------------

    public Task<List<Seminar>> GetAllForAdminAsync(CancellationToken ct = default) =>
        seminars.GetAllForAdminAsync(ct);

    public Task<Seminar?> GetForAdminEditAsync(Guid id, CancellationToken ct = default) =>
        seminars.GetWithDetailAsync(id, ct);

    public async Task<SeminarSaveResult> CreateAsync(
        Seminar seminar, SeminarTranslation defaultTranslation, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        seminar.Slug = Slugify(seminar.Slug);
        if (await seminars.SlugExistsAsync(seminar.Slug, null, ct))
            return SeminarSaveResult.Fail("Seminar.Error.SlugTaken");

        defaultTranslation.SeminarId = seminar.Id;
        defaultTranslation.Culture = SiteCultures.Default;
        defaultTranslation.BodyHtml = EditorHtml.Sanitize(defaultTranslation.BodyHtml);
        seminar.Translations.Add(defaultTranslation);

        if (seminar.Status == SeminarStatus.Published)
            seminar.PublishedAt = DateTimeOffset.UtcNow;

        await seminars.AddAsync(seminar, ct);
        await LogAsync("SeminarCreated", seminar.Id, adminUserId, ipAddress,
            before: null,
            after: new { seminar.Slug, defaultTranslation.Title, seminar.Status, seminar.Visibility, seminar.PriceMinor }, ct);
        await seminars.SaveChangesAsync(ct);

        return SeminarSaveResult.Ok(seminar.Id);
    }

    public async Task<SeminarSaveResult> UpdateAsync(Seminar updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await seminars.GetWithDetailAsync(updated.Id, ct);
        if (existing is null) return SeminarSaveResult.Fail("Seminar.Error.NotFound");

        var slug = Slugify(updated.Slug);
        if (await seminars.SlugExistsAsync(slug, existing.Id, ct))
            return SeminarSaveResult.Fail("Seminar.Error.SlugTaken");

        var before = new { existing.Slug, existing.Status, existing.Visibility, existing.PriceMinor, existing.Capacity };

        existing.Slug = slug;
        existing.Visibility = updated.Visibility;
        existing.HostName = updated.HostName;
        existing.HostTitle = updated.HostTitle;
        existing.IsOnline = updated.IsOnline;
        existing.Location = updated.Location;
        existing.TimeZoneId = updated.TimeZoneId;
        existing.StartAtUtc = updated.StartAtUtc;
        existing.EndAtUtc = updated.EndAtUtc;
        existing.Capacity = updated.Capacity;
        existing.PriceMinor = updated.PriceMinor;
        existing.Currency = updated.Currency;
        existing.IncludedWithMembership = updated.IncludedWithMembership;
        existing.SortOrder = updated.SortOrder;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        // Status deliberately excluded — publishing has its own precondition (a default-culture
        // body must exist), and routing it through SetStatusAsync means that check cannot be
        // bypassed by posting the core-fields form with Status flipped.

        await LogAsync("SeminarUpdated", existing.Id, adminUserId, ipAddress, before,
            new { existing.Slug, existing.Status, existing.Visibility, existing.PriceMinor, existing.Capacity }, ct);

        // No explicit Update() call: `existing` is already tracked on this scoped DbContext — same
        // reasoning as ExperienceService.UpdateCoreFieldsAsync.
        await seminars.SaveChangesAsync(ct);
        return SeminarSaveResult.Ok(existing.Id);
    }

    public async Task<SeminarSaveResult> SetStatusAsync(
        Guid id, SeminarStatus status, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var seminar = await seminars.GetWithDetailAsync(id, ct);
        if (seminar is null) return SeminarSaveResult.Fail("Seminar.Error.NotFound");

        if (status == SeminarStatus.Published)
        {
            // Everything else falls back to the default culture, so publishing without it would put
            // a page live that renders blank in three of the four languages.
            var fallback = SeminarContent.Find(seminar, SiteCultures.Default);
            if (fallback is null || string.IsNullOrWhiteSpace(fallback.BodyHtml))
                return SeminarSaveResult.Fail("Seminar.Error.NeedsDefaultTranslation");
        }

        var before = new { seminar.Status };
        seminar.Status = status;

        // Set once, on first publish — a later unpublish/republish cycle keeps the original date,
        // same rule as JournalPost.PublishedAt.
        if (status == SeminarStatus.Published && seminar.PublishedAt is null)
            seminar.PublishedAt = DateTimeOffset.UtcNow;

        seminar.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SeminarStatusChanged", seminar.Id, adminUserId, ipAddress, before, new { seminar.Status }, ct);
        await seminars.SaveChangesAsync(ct);

        return SeminarSaveResult.Ok(seminar.Id);
    }

    public async Task<SeminarSaveResult> DeleteAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var seminar = await seminars.GetWithDetailAsync(id, ct);
        if (seminar is null) return SeminarSaveResult.Fail("Seminar.Error.NotFound");

        // A seminar somebody enrolled on is a financial and access record. Archiving keeps their
        // place working; deleting would strip it silently, and the FK is Restrict precisely so this
        // cannot happen by accident.
        if (await enrollments.CountTakenAsync(id, ct) > 0)
            return SeminarSaveResult.Fail("Seminar.Error.HasEnrolments");

        // Captured before the delete, since the navigation is cleared as part of it.
        var storageKeys = seminar.Media.Select(m => m.StorageKey).ToList();

        seminars.Remove(seminar);
        await LogAsync("SeminarDeleted", seminar.Id, adminUserId, ipAddress,
            before: new { seminar.Slug, seminar.Status, MediaCount = storageKeys.Count }, after: null, ct);
        await seminars.SaveChangesAsync(ct);

        // Files last, for the same reason as RemoveMediaAsync: if the delete is refused by a
        // constraint we did not anticipate, the seminar survives with its media intact rather than
        // staying live with every asset already erased.
        foreach (var key in storageKeys)
            await mediaStorage.DeleteAsync(key, ct);

        return SeminarSaveResult.Ok();
    }

    // --- Admin: translations -------------------------------------------------------------------

    public async Task<SeminarSaveResult> SaveTranslationAsync(
        Guid seminarId, SeminarTranslation translation, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var seminar = await seminars.GetWithDetailAsync(seminarId, ct);
        if (seminar is null) return SeminarSaveResult.Fail("Seminar.Error.NotFound");

        var culture = SiteCultures.Normalise(translation.Culture);
        var body = EditorHtml.Sanitize(translation.BodyHtml);

        var existing = SeminarContent.Find(seminar, culture);
        if (existing is null)
        {
            await translations.AddAsync(new SeminarTranslation
            {
                SeminarId = seminarId,
                Culture = culture,
                Title = translation.Title.Trim(),
                Summary = translation.Summary,
                BodyHtml = body,
                SeoTitle = translation.SeoTitle,
                SeoDescription = translation.SeoDescription,
            }, ct);
        }
        else
        {
            existing.Title = translation.Title.Trim();
            existing.Summary = translation.Summary;
            existing.BodyHtml = body;
            existing.SeoTitle = translation.SeoTitle;
            existing.SeoDescription = translation.SeoDescription;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        seminar.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SeminarTranslationSaved", seminar.Id, adminUserId, ipAddress,
            before: existing is null ? null : new { existing.Culture, existing.Title },
            after: new { Culture = culture, translation.Title }, ct);
        await seminars.SaveChangesAsync(ct);

        return SeminarSaveResult.Ok(seminar.Id);
    }

    public async Task<SeminarSaveResult> DeleteTranslationAsync(
        Guid seminarId, string culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var seminar = await seminars.GetWithDetailAsync(seminarId, ct);
        if (seminar is null) return SeminarSaveResult.Fail("Seminar.Error.NotFound");

        var normalised = SiteCultures.Normalise(culture);
        if (string.Equals(normalised, SiteCultures.Default, StringComparison.OrdinalIgnoreCase))
            return SeminarSaveResult.Fail("Seminar.Error.CannotDeleteDefaultTranslation");

        var existing = SeminarContent.Find(seminar, normalised);
        if (existing is null) return SeminarSaveResult.Ok(seminar.Id);

        translations.Remove(existing);
        seminar.Translations.Remove(existing);
        seminar.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SeminarTranslationDeleted", seminar.Id, adminUserId, ipAddress,
            before: new { existing.Culture, existing.Title }, after: null, ct);
        await seminars.SaveChangesAsync(ct);

        return SeminarSaveResult.Ok(seminar.Id);
    }

    // --- Admin: media --------------------------------------------------------------------------

    public async Task<SeminarMediaResult> AddMediaAsync(
        Guid seminarId, MediaUpload upload, string? title, bool isInline,
        Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var seminar = await seminars.GetWithDetailAsync(seminarId, ct);
        if (seminar is null) return SeminarMediaResult.Fail("Seminar.Error.NotFound");

        // Classified from the extension, not from the browser's declared content type — see
        // MediaPolicy. A null here means the file is simply not something we serve.
        var kind = MediaPolicy.Classify(upload.FileName);
        if (kind is null) return SeminarMediaResult.Fail("Seminar.Error.MediaType");

        if (upload.Length > MediaPolicy.MaxBytesFor(kind.Value))
            return SeminarMediaResult.Fail("Seminar.Error.MediaTooLarge");

        var saved = await mediaStorage.SaveAsync(upload, $"seminars/{seminarId:N}", ct);
        if (!saved.Success) return SeminarMediaResult.Fail(saved.Error ?? "Seminar.Error.MediaFailed");

        var media = new SeminarMedia
        {
            SeminarId = seminarId,
            StorageKey = saved.StorageKey!,
            Kind = kind.Value,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            IsInline = isInline,
            ContentType = saved.ContentType!,
            SizeBytes = saved.SizeBytes,
            OriginalFileName = upload.FileName,
            SortOrder = seminar.Media.Count == 0 ? 1 : seminar.Media.Max(m => m.SortOrder) + 1,
        };
        await mediaRows.AddAsync(media, ct);

        // The first still image uploaded through the media panel becomes the cover, because a
        // seminar with no cover renders as a grey rectangle and nobody remembers to come back and
        // pick one. Inline uploads are excluded: a diagram dropped mid-article is not the poster.
        if (seminar.CoverMediaId is null && !isInline && kind.Value == SeminarMediaKind.Image)
            seminar.CoverMediaId = media.Id;

        seminar.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SeminarMediaAdded", seminar.Id, adminUserId, ipAddress, before: null,
            after: new { media.StorageKey, Kind = kind.Value.ToString(), saved.SizeBytes, upload.FileName, isInline }, ct);

        try
        {
            await seminars.SaveChangesAsync(ct);
        }
        catch
        {
            // The bytes are already on disk by this point. Without this, a failed save leaves a file
            // nothing references and nothing will ever clean up — which is how a media directory
            // quietly fills with the debris of every error. Put back and rethrow.
            await mediaStorage.DeleteAsync(saved.StorageKey!, ct);
            throw;
        }

        return SeminarMediaResult.Ok(media);
    }

    public async Task<SeminarSaveResult> RemoveMediaAsync(
        Guid seminarId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var seminar = await seminars.GetWithDetailAsync(seminarId, ct);
        if (seminar is null) return SeminarSaveResult.Fail("Seminar.Error.NotFound");

        var media = seminar.Media.FirstOrDefault(m => m.Id == mediaId);
        if (media is null) return SeminarSaveResult.Ok(seminar.Id); // already gone — a double submit

        mediaRows.Remove(media);

        // Clear the pointer rather than leave it dangling. The read side tolerates a stale id, but
        // an admin who deletes the cover should see the next one they choose actually take effect.
        // Filtered against the row being removed, since it is still in the loaded navigation.
        if (seminar.CoverMediaId == mediaId)
        {
            seminar.CoverMediaId = seminar.Media
                .FirstOrDefault(m => m.Id != mediaId && m.Kind == SeminarMediaKind.Image && !m.IsInline)?.Id;
        }

        seminar.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SeminarMediaRemoved", seminar.Id, adminUserId, ipAddress,
            before: new { media.StorageKey, Kind = media.Kind.ToString() }, after: null, ct);
        await seminars.SaveChangesAsync(ct);

        // Deleted only once the row is definitely gone. The other order risks a failed save leaving
        // a row that points at a file which no longer exists — a broken player on a live page, which
        // is worse than an orphaned file nobody sees.
        await mediaStorage.DeleteAsync(media.StorageKey, ct);

        return SeminarSaveResult.Ok(seminar.Id);
    }

    public async Task<SeminarSaveResult> SetCoverAsync(
        Guid seminarId, Guid mediaId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var seminar = await seminars.GetWithDetailAsync(seminarId, ct);
        if (seminar is null) return SeminarSaveResult.Fail("Seminar.Error.NotFound");

        // Checked against this seminar's own media, so a posted id from another seminar cannot make
        // one session's cover serve another's (possibly members-only) image.
        var media = seminar.Media.FirstOrDefault(m => m.Id == mediaId);
        if (media is null) return SeminarSaveResult.Fail("Seminar.Error.MediaNotFound");
        if (media.Kind is not (SeminarMediaKind.Image or SeminarMediaKind.Animation))
            return SeminarSaveResult.Fail("Seminar.Error.CoverMustBeImage");

        var before = new { seminar.CoverMediaId };
        seminar.CoverMediaId = mediaId;
        seminar.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SeminarCoverChanged", seminar.Id, adminUserId, ipAddress, before, new { seminar.CoverMediaId }, ct);
        await seminars.SaveChangesAsync(ct);

        return SeminarSaveResult.Ok(seminar.Id);
    }

    // --- Media streaming (access-checked) --------------------------------------------------------

    public async Task<MediaFileInfo?> OpenMediaAsync(
        Guid mediaId, Guid? userId, bool viewerIsMember, bool viewerIsStaff, CancellationToken ct = default)
    {
        var seminar = await seminars.GetByMediaIdAsync(mediaId, ct);
        if (seminar is null) return null;

        var media = seminar.Media.First(m => m.Id == mediaId);

        if (!viewerIsStaff)
        {
            // Draft is admin-only, and a members-only session's assets are not served to a viewer
            // who could not have reached its page in the first place.
            if (seminar.Status == SeminarStatus.Draft) return null;
            if (seminar.Visibility == SeminarVisibility.Members && !viewerIsMember) return null;

            // The cover is the one asset that is not behind enrolment — it is on the listing card,
            // so anyone who can see the card can already see it.
            if (seminar.CoverMediaId != mediaId)
            {
                var access = await GetAccessAsync(seminar, userId, ct);
                if (!access.HasAccess) return null;
            }
        }

        return await mediaStorage.GetAsync(media.StorageKey, ct);
    }

    public Task<List<SeminarEnrollment>> GetEnrollmentsForAdminAsync(Guid seminarId, CancellationToken ct = default) =>
        enrollments.GetForSeminarAsync(seminarId, ct);

    // --- Helpers -------------------------------------------------------------------------------

    /// <summary>Null when the seminar has no capacity limit; otherwise never below zero.</summary>
    private async Task<int?> SeatsRemainingAsync(Seminar seminar, CancellationToken ct)
    {
        if (seminar.Capacity <= 0) return null;

        var taken = await enrollments.CountTakenAsync(seminar.Id, ct);
        return Math.Max(0, seminar.Capacity - taken);
    }

    /// <summary>
    /// Returns the one enrolment row for this pair, creating it if this is a first attempt. Reusing
    /// the row is what makes a retry after an abandoned checkout work at all — the unique index on
    /// (SeminarId, UserId) would reject a second one.
    /// </summary>
    private async Task<SeminarEnrollment> UpsertEnrollmentAsync(Guid seminarId, Guid userId, CancellationToken ct)
    {
        var existing = await enrollments.GetForUserAsync(seminarId, userId, ct);
        if (existing is not null) return existing;

        var created = new SeminarEnrollment { SeminarId = seminarId, UserId = userId };
        await enrollments.AddAsync(created, ct);
        return created;
    }

    /// <summary>
    /// Tells someone they are in — in-app and by email, both best-effort by the contract of
    /// INotificationService/IEmailService, so a dead SMTP host can never undo a confirmed enrolment.
    /// </summary>
    private async Task AnnounceEnrolmentAsync(Seminar seminar, Guid userId, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return;

        var title = SeminarContent.Title(seminar, SiteCultures.Default);
        var link = $"/sessions/{seminar.Slug}";

        await notificationService.CreateForUserAsync(
            userId, NotificationType.SeminarEnrolled,
            "You're enrolled", $"Your place on \"{title}\" is confirmed.", link, ct);

        await emailService.SendAsync(
            "SeminarEnrolled", user.Email!, $"You're enrolled — {title}",
            new SeminarEnrolledEmailModel(
                user.FirstName, title, seminar.StartAtUtc, seminar.IsOnline, seminar.Location,
                $"{siteOptions.Value.BaseUrl.TrimEnd('/')}{link}"),
            nameof(Seminar), seminar.Id, ct);
    }

    /// <summary>
    /// Normalises whatever the admin typed into a URL-safe slug. Deliberately conservative — ASCII
    /// letters, digits and single hyphens — because this ends up in a route, in emails and in links
    /// people paste, and a Turkish "ı" or a German "ß" surviving into a URL is a support ticket
    /// waiting to happen. The title keeps the real characters; only the slug is flattened.
    /// </summary>
    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("ı", "i").Replace("ş", "s").Replace("ğ", "g").Replace("ü", "u")
            .Replace("ö", "o").Replace("ç", "c").Replace("ä", "ae").Replace("ß", "ss")
            .Replace("õ", "o");

        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsAsciiLetterOrDigit(ch)) builder.Append(ch);
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        }

        return builder.ToString().Trim('-');
    }

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(Seminar),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
