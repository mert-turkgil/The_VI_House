using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Concrete;

public class ExperienceService(
    IExperienceRepository experiences,
    ITicketTypeRepository ticketTypes,
    IRepository<ExperienceInclusion> inclusions,
    IRepository<ExperienceFaq> faqs,
    IRepository<ExperienceImage> gallery,
    IRepository<ExperienceProgramDay> programDays,
    IRepository<ExperienceSession> sessions,
    IRepository<ExperienceMembershipAccess> membershipAccess,
    IBookingRepository bookings,
    IMembershipService membershipService,
    IMediaStorage mediaStorage,
    IAuditLogRepository auditLogs) : IExperienceService
{
    /// <summary>Uploaded files are grouped per experience, matching JournalService's journal/{id:N}.</summary>
    private static string StorageFolder(Guid experienceId) => $"experience/{experienceId:N}";

    /// <summary>
    /// Where to actually show an experience's cover from, resolving the URL/upload pair in one place
    /// so the card, the detail page and the admin preview can never disagree about which wins.
    ///
    /// Built as a literal path rather than through Url.Action because the callers are static
    /// FromEntity mappers with no access to a UrlHelper — the same reason JournalService.MediaUrl
    /// exists. The version stamp makes a replaced image arrive on a new URL, so MediaController can
    /// cache hard.
    /// </summary>
    public static string? CoverUrl(Experience experience) =>
        experience.CoverImageStorageKey is null
            ? experience.CoverImageUrl
            : $"/media/experience/{experience.Id}?v={Stamp(experience.UpdatedAt, experience.CreatedAt)}";

    public static string? GalleryImageUrl(ExperienceImage image) =>
        image.StorageKey is null
            ? image.Url
            : $"/media/experience-gallery/{image.Id}?v={Stamp(image.UpdatedAt, image.CreatedAt)}";

    private static long Stamp(DateTimeOffset? updated, DateTimeOffset created) =>
        (updated ?? created).ToUnixTimeSeconds();

    public Task<List<Experience>> GetPublicListingAsync(ExperienceFilter filter, CancellationToken ct = default) =>
        experiences.GetPublicListingAsync(filter, ct);

    public Task<Experience?> GetPublicDetailBySlugAsync(string slug, CancellationToken ct = default) =>
        experiences.GetWithDetailsBySlugAsync(slug, ct);

    public Task<List<Experience>> GetUpcomingAsync(int take, CancellationToken ct = default) =>
        experiences.GetUpcomingAsync(take, ct);

    public Task<List<Experience>> GetSignatureAsync(int take, CancellationToken ct = default) =>
        experiences.GetSignatureAsync(take, ct);

    public Task<List<string>> GetPublicCitiesAsync(CancellationToken ct = default) =>
        experiences.GetPublicCitiesAsync(ct);

    public Task<List<Experience>> GetAllForAdminAsync(CancellationToken ct = default) =>
        experiences.GetAllAsync(ct);

    public Task<Experience?> GetForAdminEditAsync(Guid id, CancellationToken ct = default) =>
        experiences.GetWithDetailsAsync(id, ct);

    public async Task<Experience> CreateAsync(Experience experience, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        await experiences.AddAsync(experience, ct);
        await LogAsync("ExperienceCreated", nameof(Experience), experience.Id, adminUserId, ipAddress,
            before: null, after: new { experience.Title, experience.Slug, experience.Status }, ct);
        await experiences.SaveChangesAsync(ct);
        return experience;
    }

    public async Task UpdateCoreFieldsAsync(Experience updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await experiences.GetByIdAsync(updated.Id, ct)
            ?? throw new InvalidOperationException($"Experience {updated.Id} not found.");

        var before = new { existing.Title, existing.Slug, existing.Status, existing.Visibility };

        existing.Title = updated.Title;
        existing.Slug = updated.Slug;
        existing.ShortSummary = updated.ShortSummary;
        existing.Description = updated.Description;
        existing.City = updated.City;
        existing.Country = updated.Country;
        existing.Venue = updated.Venue;
        existing.TimeZoneId = updated.TimeZoneId;
        existing.StartAtUtc = updated.StartAtUtc;
        existing.EndAtUtc = updated.EndAtUtc;
        existing.Capacity = updated.Capacity;
        existing.Status = updated.Status;
        existing.Visibility = updated.Visibility;
        existing.AttendanceMode = updated.AttendanceMode;
        existing.CoverImageUrl = updated.CoverImageUrl;
        existing.ApplicationOpenAt = updated.ApplicationOpenAt;
        existing.ApplicationCloseAt = updated.ApplicationCloseAt;
        existing.SalesOpenAt = updated.SalesOpenAt;
        existing.SalesCloseAt = updated.SalesCloseAt;
        existing.SeoTitle = updated.SeoTitle;
        existing.SeoDescription = updated.SeoDescription;
        // These three were on the entity and rendered on the public page, but were copied by nothing
        // and bound by no form — so the audience chips, the cover's alt text and the social preview
        // image could only ever be set by the seeder.
        existing.SeoOgImageUrl = updated.SeoOgImageUrl;
        existing.CoverImageAlt = updated.CoverImageAlt;
        existing.AudienceTags = updated.AudienceTags;
        existing.IsSignature = updated.IsSignature;
        existing.SortOrder = updated.SortOrder;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("ExperienceUpdated", nameof(Experience), existing.Id, adminUserId, ipAddress,
            before, new { existing.Title, existing.Slug, existing.Status, existing.Visibility }, ct);

        // No explicit Update() call: `existing` was loaded via GetByIdAsync on this same scoped
        // DbContext, so it's already tracked — EF's change tracker detects the property
        // assignments above automatically at SaveChanges time.
        await experiences.SaveChangesAsync(ct);
    }

    public async Task<bool> TryDeleteAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await experiences.GetByIdAsync(id, ct);
        if (existing is null) return true;

        experiences.Remove(existing);
        try
        {
            await LogAsync("ExperienceDeleted", nameof(Experience), id, adminUserId, ipAddress,
                before: new { existing.Title, existing.Slug }, after: null, ct);
            await experiences.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Applications/Bookings/Payments reference this experience with Restrict delete
            // behavior on purpose (brief §98) — surface as "can't delete", not a 500.
            return false;
        }
    }

    public async Task AddTicketTypeAsync(Guid experienceId, TicketType ticketType, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null)
            throw new InvalidOperationException($"Experience {experienceId} not found.");

        // Inserted via ITicketTypeRepository.AddAsync (DbSet.Add) rather than by pushing into the
        // loaded Experience's TicketTypes navigation collection: EF Core can't reliably tell a
        // "new" entity from an "existing" one purely from graph discovery once the entity already
        // has a non-default (client-generated) Guid key set — it treated the new row as an UPDATE
        // and threw a bogus DbUpdateConcurrencyException. DbSet.Add() marks the state Added
        // unambiguously. Same reasoning applies to inclusions/FAQs below.
        ticketType.ExperienceId = experienceId;
        await ticketTypes.AddAsync(ticketType, ct);
        await LogAsync("TicketTypeAdded", nameof(TicketType), ticketType.Id, adminUserId, ipAddress,
            before: null, after: new { ticketType.Title, ticketType.PriceMinor, ticketType.Currency, ticketType.Inventory, ExperienceId = experienceId }, ct);
        await ticketTypes.SaveChangesAsync(ct);
    }

    public async Task<bool> TryRemoveTicketTypeAsync(Guid experienceId, Guid ticketTypeId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var ticketType = await ticketTypes.GetByIdAsync(ticketTypeId, ct);
        if (ticketType is null || ticketType.ExperienceId != experienceId) return true;

        ticketTypes.Remove(ticketType);
        try
        {
            await LogAsync("TicketTypeRemoved", nameof(TicketType), ticketTypeId, adminUserId, ipAddress,
                before: new { ticketType.Title, ExperienceId = experienceId }, after: null, ct);
            await ticketTypes.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Bookings/Payments/TicketHolds reference this ticket type — can't remove it once sold.
            return false;
        }
    }

    public async Task AddInclusionAsync(Guid experienceId, ExperienceInclusion inclusion, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null)
            throw new InvalidOperationException($"Experience {experienceId} not found.");

        inclusion.ExperienceId = experienceId;
        await inclusions.AddAsync(inclusion, ct);
        await LogAsync("InclusionAdded", nameof(ExperienceInclusion), inclusion.Id, adminUserId, ipAddress,
            before: null, after: new { inclusion.Text, inclusion.IsIncluded, ExperienceId = experienceId }, ct);
        await inclusions.SaveChangesAsync(ct);
    }

    public async Task RemoveInclusionAsync(Guid experienceId, Guid inclusionId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var inclusion = await inclusions.GetByIdAsync(inclusionId, ct);
        if (inclusion is null || inclusion.ExperienceId != experienceId) return;

        inclusions.Remove(inclusion);
        await LogAsync("InclusionRemoved", nameof(ExperienceInclusion), inclusionId, adminUserId, ipAddress,
            before: new { inclusion.Text, ExperienceId = experienceId }, after: null, ct);
        await inclusions.SaveChangesAsync(ct);
    }

    public async Task AddFaqAsync(Guid experienceId, ExperienceFaq faq, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null)
            throw new InvalidOperationException($"Experience {experienceId} not found.");

        faq.ExperienceId = experienceId;
        await faqs.AddAsync(faq, ct);
        await LogAsync("FaqAdded", nameof(ExperienceFaq), faq.Id, adminUserId, ipAddress,
            before: null, after: new { faq.Question, ExperienceId = experienceId }, ct);
        await faqs.SaveChangesAsync(ct);
    }

    public async Task RemoveFaqAsync(Guid experienceId, Guid faqId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var faq = await faqs.GetByIdAsync(faqId, ct);
        if (faq is null || faq.ExperienceId != experienceId) return;

        faqs.Remove(faq);
        await LogAsync("FaqRemoved", nameof(ExperienceFaq), faqId, adminUserId, ipAddress,
            before: new { faq.Question, ExperienceId = experienceId }, after: null, ct);
        await faqs.SaveChangesAsync(ct);
    }

    // --- Editing child rows -------------------------------------------------------------------------

    public async Task<string?> UpdateTicketTypeAsync(Guid experienceId, TicketType updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await ticketTypes.GetByIdAsync(updated.Id, ct);
        if (existing is null || existing.ExperienceId != experienceId) return "Admin.Experience.TicketTypeNotFound";

        var before = new { existing.Title, existing.PriceMinor, existing.Inventory };

        existing.Title = updated.Title;
        existing.Description = updated.Description;
        existing.PriceMinor = updated.PriceMinor;
        existing.Currency = updated.Currency;
        existing.PerksText = updated.PerksText;
        existing.MaxQuantityPerOrder = updated.MaxQuantityPerOrder;
        existing.SortOrder = updated.SortOrder;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        // Inventory is deliberately not assigned here. It is the no-oversell counter and is only ever
        // moved by CapacityService's atomic conditional UPDATE; writing it from a form would let a
        // stale page hand back a number from before three people bought tickets.

        await LogAsync("TicketTypeUpdated", nameof(TicketType), existing.Id, adminUserId, ipAddress,
            before, new { existing.Title, existing.PriceMinor }, ct);
        await ticketTypes.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> UpdateInclusionAsync(Guid experienceId, Guid inclusionId, string text, bool isIncluded, int sortOrder, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await inclusions.GetByIdAsync(inclusionId, ct);
        if (existing is null || existing.ExperienceId != experienceId) return "Admin.Experience.RowNotFound";

        var before = new { existing.Text, existing.IsIncluded };
        existing.Text = text;
        existing.IsIncluded = isIncluded;
        existing.SortOrder = sortOrder;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("InclusionUpdated", nameof(ExperienceInclusion), inclusionId, adminUserId, ipAddress,
            before, new { existing.Text, existing.IsIncluded }, ct);
        await inclusions.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> UpdateFaqAsync(Guid experienceId, Guid faqId, string question, string answer, int sortOrder, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await faqs.GetByIdAsync(faqId, ct);
        if (existing is null || existing.ExperienceId != experienceId) return "Admin.Experience.RowNotFound";

        var before = new { existing.Question };
        existing.Question = question;
        existing.Answer = answer;
        existing.SortOrder = sortOrder;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("FaqUpdated", nameof(ExperienceFaq), faqId, adminUserId, ipAddress,
            before, new { existing.Question }, ct);
        await faqs.SaveChangesAsync(ct);
        return null;
    }

    // --- Programme ------------------------------------------------------------------------------------

    public async Task AddProgramDayAsync(Guid experienceId, ExperienceProgramDay day, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null)
            throw new InvalidOperationException($"Experience {experienceId} not found.");

        day.ExperienceId = experienceId;
        await programDays.AddAsync(day, ct);
        await LogAsync("ProgramDayAdded", nameof(ExperienceProgramDay), day.Id, adminUserId, ipAddress,
            before: null, after: new { day.DayNumber, day.Title, ExperienceId = experienceId }, ct);
        await programDays.SaveChangesAsync(ct);
    }

    public async Task RemoveProgramDayAsync(Guid experienceId, Guid dayId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var day = await programDays.GetByIdAsync(dayId, ct);
        if (day is null || day.ExperienceId != experienceId) return;

        // The day's sessions go with it. Cascade delete covers this in the database, but the rows are
        // removed explicitly so the tracked graph matches what the database will end up with.
        foreach (var session in await sessions.FindAsync(s => s.ProgramDayId == dayId, ct))
            sessions.Remove(session);

        programDays.Remove(day);
        await LogAsync("ProgramDayRemoved", nameof(ExperienceProgramDay), dayId, adminUserId, ipAddress,
            before: new { day.DayNumber, day.Title, ExperienceId = experienceId }, after: null, ct);
        await programDays.SaveChangesAsync(ct);
    }

    public async Task<string?> AddSessionAsync(Guid experienceId, Guid dayId, ExperienceSession session, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var day = await programDays.GetByIdAsync(dayId, ct);
        if (day is null || day.ExperienceId != experienceId) return "Admin.Experience.RowNotFound";

        session.ProgramDayId = dayId;
        await sessions.AddAsync(session, ct);
        await LogAsync("SessionAdded", nameof(ExperienceSession), session.Id, adminUserId, ipAddress,
            before: null, after: new { session.Title, session.StartTime, ProgramDayId = dayId }, ct);
        await sessions.SaveChangesAsync(ct);
        return null;
    }

    public async Task RemoveSessionAsync(Guid experienceId, Guid sessionId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var session = await sessions.GetByIdAsync(sessionId, ct);
        if (session is null) return;

        // The session knows its day, not its experience — so confirm the day belongs here before
        // deleting, or a crafted id could remove a session from someone else's experience.
        var day = await programDays.GetByIdAsync(session.ProgramDayId, ct);
        if (day is null || day.ExperienceId != experienceId) return;

        sessions.Remove(session);
        await LogAsync("SessionRemoved", nameof(ExperienceSession), sessionId, adminUserId, ipAddress,
            before: new { session.Title, session.ProgramDayId }, after: null, ct);
        await sessions.SaveChangesAsync(ct);
    }

    // --- Images ---------------------------------------------------------------------------------------

    public async Task<string?> UploadCoverAsync(Guid experienceId, MediaUpload upload, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var experience = await experiences.GetByIdAsync(experienceId, ct);
        if (experience is null) return "Admin.Experience.NotFound";

        if (MediaPolicy.Classify(upload.FileName) is not SeminarMediaKind.Image)
            return "Admin.Experience.ImageTypeOnly";

        var saved = await mediaStorage.SaveAsync(upload, StorageFolder(experienceId), ct);
        if (!saved.Success) return saved.Error;

        var previous = experience.CoverImageStorageKey;
        experience.CoverImageStorageKey = saved.StorageKey;
        experience.CoverImageUrl = null; // an upload wins outright — see the entity
        experience.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("ExperienceCoverUploaded", nameof(Experience), experienceId, adminUserId, ipAddress,
            before: new { Previous = previous }, after: new { saved.StorageKey }, ct);

        try
        {
            await experiences.SaveChangesAsync(ct);
        }
        catch
        {
            // The row did not change, so the file we just wrote belongs to nothing. Delete it rather
            // than leave an orphan on disk that no screen can ever reach.
            await mediaStorage.DeleteAsync(saved.StorageKey!, ct);
            throw;
        }

        // Only after the commit: deleting first would lose the old cover if the save then failed.
        if (previous is not null)
            await mediaStorage.DeleteAsync(previous, ct);

        return null;
    }

    public async Task RemoveCoverAsync(Guid experienceId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var experience = await experiences.GetByIdAsync(experienceId, ct);
        if (experience?.CoverImageStorageKey is not { } key) return;

        experience.CoverImageStorageKey = null;
        experience.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("ExperienceCoverRemoved", nameof(Experience), experienceId, adminUserId, ipAddress,
            before: new { StorageKey = key }, after: null, ct);
        await experiences.SaveChangesAsync(ct);
        await mediaStorage.DeleteAsync(key, ct);
    }

    public async Task<string?> AddGalleryImageAsync(Guid experienceId, MediaUpload upload, string? altText, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null) return "Admin.Experience.NotFound";

        if (MediaPolicy.Classify(upload.FileName) is not SeminarMediaKind.Image)
            return "Admin.Experience.ImageTypeOnly";

        var saved = await mediaStorage.SaveAsync(upload, StorageFolder(experienceId), ct);
        if (!saved.Success) return saved.Error;

        var image = new ExperienceImage
        {
            ExperienceId = experienceId,
            StorageKey = saved.StorageKey,
            AltText = altText,
            SortOrder = await NextGallerySortOrderAsync(experienceId, ct),
        };

        await gallery.AddAsync(image, ct);
        await LogAsync("GalleryImageAdded", nameof(ExperienceImage), image.Id, adminUserId, ipAddress,
            before: null, after: new { saved.StorageKey, ExperienceId = experienceId }, ct);

        try
        {
            await gallery.SaveChangesAsync(ct);
        }
        catch
        {
            await mediaStorage.DeleteAsync(saved.StorageKey!, ct);
            throw;
        }

        return null;
    }

    public async Task<string?> AddGalleryImageByUrlAsync(Guid experienceId, string url, string? altText, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (await experiences.GetByIdAsync(experienceId, ct) is null) return "Admin.Experience.NotFound";

        var image = new ExperienceImage
        {
            ExperienceId = experienceId,
            Url = url,
            AltText = altText,
            SortOrder = await NextGallerySortOrderAsync(experienceId, ct),
        };

        await gallery.AddAsync(image, ct);
        await LogAsync("GalleryImageAdded", nameof(ExperienceImage), image.Id, adminUserId, ipAddress,
            before: null, after: new { Url = url, ExperienceId = experienceId }, ct);
        await gallery.SaveChangesAsync(ct);
        return null;
    }

    public async Task UpdateGalleryImageAsync(Guid experienceId, Guid imageId, string? altText, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var image = await gallery.GetByIdAsync(imageId, ct);
        if (image is null || image.ExperienceId != experienceId) return;

        image.AltText = altText;
        image.UpdatedAt = DateTimeOffset.UtcNow;
        await LogAsync("GalleryImageUpdated", nameof(ExperienceImage), imageId, adminUserId, ipAddress,
            before: null, after: new { AltText = altText }, ct);
        await gallery.SaveChangesAsync(ct);
    }

    public async Task RemoveGalleryImageAsync(Guid experienceId, Guid imageId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var image = await gallery.GetByIdAsync(imageId, ct);
        if (image is null || image.ExperienceId != experienceId) return;

        var key = image.StorageKey;
        gallery.Remove(image);
        await LogAsync("GalleryImageRemoved", nameof(ExperienceImage), imageId, adminUserId, ipAddress,
            before: new { image.Url, image.StorageKey }, after: null, ct);
        await gallery.SaveChangesAsync(ct);

        // A typed URL points at a file committed under wwwroot that other rows may share, so only an
        // uploaded file — which this row alone owns — is deleted from disk.
        if (key is not null)
            await mediaStorage.DeleteAsync(key, ct);
    }

    public async Task MoveGalleryImageAsync(Guid experienceId, Guid imageId, int delta, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var images = (await gallery.FindAsync(g => g.ExperienceId == experienceId, ct))
            .OrderBy(g => g.SortOrder).ThenBy(g => g.CreatedAt)
            .ToList();

        var index = images.FindIndex(g => g.Id == imageId);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= images.Count) return;

        // Rewritten as a dense 1..n sequence rather than swapping two values, because seeded rows do
        // not necessarily start at 1 or increment by 1 and a bare swap would preserve any gaps.
        (images[index], images[target]) = (images[target], images[index]);
        for (var i = 0; i < images.Count; i++)
        {
            images[i].SortOrder = i + 1;
            images[i].UpdatedAt = DateTimeOffset.UtcNow;
        }

        await LogAsync("GalleryImageMoved", nameof(ExperienceImage), imageId, adminUserId, ipAddress,
            before: null, after: new { Delta = delta }, ct);
        await gallery.SaveChangesAsync(ct);
    }

    // --- Member access ----------------------------------------------------------------------------------

    public async Task<List<Guid>> GetMembershipAccessAsync(Guid experienceId, CancellationToken ct = default) =>
        [.. (await membershipAccess.FindAsync(a => a.ExperienceId == experienceId, ct)).Select(a => a.MembershipPlanId)];

    public async Task SetMembershipAccessAsync(Guid experienceId, IReadOnlyCollection<Guid> planIds, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await membershipAccess.FindAsync(a => a.ExperienceId == experienceId, ct);
        var wanted = planIds.ToHashSet();

        foreach (var row in existing.Where(a => !wanted.Contains(a.MembershipPlanId)))
            membershipAccess.Remove(row);

        var already = existing.Select(a => a.MembershipPlanId).ToHashSet();
        foreach (var planId in wanted.Where(p => !already.Contains(p)))
            await membershipAccess.AddAsync(new ExperienceMembershipAccess
            {
                ExperienceId = experienceId,
                MembershipPlanId = planId,
            }, ct);

        await LogAsync("ExperienceMembershipAccessSet", nameof(Experience), experienceId, adminUserId, ipAddress,
            before: new { Plans = already.ToArray() }, after: new { Plans = wanted.ToArray() }, ct);
        await membershipAccess.SaveChangesAsync(ct);
    }

    public async Task<ExperienceAccessInfo> GetAccessAsync(Experience experience, Guid? userId, CancellationToken ct = default)
    {
        // An existing booking outranks everything below it, including an experience that has since
        // closed: somebody who already has a place keeps it. Same rule as SeminarService.
        if (userId is { } id)
        {
            var mine = await bookings.FindAsync(
                b => b.UserId == id && b.ExperienceId == experience.Id && b.Status != BookingStatus.Cancelled, ct);

            if (mine.FirstOrDefault() is { } booking)
                return new ExperienceAccessInfo(ExperienceAccessOutcome.AlreadyBooked, booking.BookingReference);
        }

        var admittedPlans = await GetMembershipAccessAsync(experience.Id, ct);

        // No plan is admitted, so membership is not a route in and the ordinary rules apply.
        if (admittedPlans.Count == 0)
            return new ExperienceAccessInfo(IsOpen(experience)
                ? ExperienceAccessOutcome.RequiresApplication
                : ExperienceAccessOutcome.NotOpen);

        if (!IsOpen(experience))
            return new ExperienceAccessInfo(ExperienceAccessOutcome.NotOpen);

        // Worth prompting a sign-in only when membership could actually change the answer.
        if (userId is null)
            return new ExperienceAccessInfo(ExperienceAccessOutcome.NeedsSignIn);

        // Read live rather than from a claim, so a lapsed membership stops opening the door the
        // moment it lapses rather than at their next sign-in.
        var membership = await membershipService.GetCurrentMembershipAsync(userId.Value, ct);

        return new ExperienceAccessInfo(membership is not null && admittedPlans.Contains(membership.PlanId)
            ? ExperienceAccessOutcome.IncludedInMembership
            : ExperienceAccessOutcome.RequiresApplication);
    }

    public async Task<(string? Error, string? BookingReference)> JoinAsMemberAsync(
        Guid experienceId, Guid userId, BookingAttendance? attendance, CancellationToken ct = default)
    {
        var experience = await experiences.GetByIdAsync(experienceId, ct);
        if (experience is null) return ("Experiences.Join.NotFound", null);

        // Re-derived here rather than trusted from the form: the page may have been rendered while
        // this plan was admitted, or before the membership lapsed, and the server is the only place
        // that can safely decide.
        var access = await GetAccessAsync(experience, userId, ct);

        if (access.Outcome == ExperienceAccessOutcome.AlreadyBooked)
            return ("Experiences.Join.AlreadyBooked", access.BookingReference);

        if (access.Outcome != ExperienceAccessOutcome.IncludedInMembership)
            return ("Experiences.Join.NotEntitled", null);

        var booking = new Booking
        {
            BookingReference = await bookings.GenerateNextReferenceAsync(DateTimeOffset.UtcNow.Year % 100, ct),
            UserId = userId,
            ExperienceId = experienceId,
            // No ticket type and no application: nothing was bought and no form was filled in. No
            // capacity hold either — the House does not sell seats for an experience, so there is
            // nothing to reserve and nothing a member could take from a paying guest.
            TicketTypeId = null,
            ApplicationId = null,
            Quantity = 1,
            AmountMinor = 0,
            Currency = experience.TicketTypes.FirstOrDefault()?.Currency ?? "GBP",
            Status = BookingStatus.Confirmed,
            GrantedVia = BookingGrant.Membership,
            Attendance = experience.AttendanceMode == ExperienceAttendanceMode.Both
                ? attendance ?? BookingAttendance.InPerson
                : experience.AttendanceMode == ExperienceAttendanceMode.Online
                    ? BookingAttendance.Online
                    : BookingAttendance.InPerson,
            ConfirmedAt = DateTimeOffset.UtcNow,
        };

        await bookings.AddAsync(booking, ct);

        try
        {
            await bookings.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index on (ExperienceId, UserId) caught a double submit — two clicks, or two
            // tabs. Report it as "you already have a place", which is true, rather than as a failure.
            return ("Experiences.Join.AlreadyBooked", null);
        }

        return (null, booking.BookingReference);
    }

    /// <summary>Open to be joined or applied for at all — the same test the Apply button uses.</summary>
    private static bool IsOpen(Experience experience) =>
        experience.Status is ExperienceStatus.ApplicationsOpen or ExperienceStatus.AlmostFull;

    private async Task<int> NextGallerySortOrderAsync(Guid experienceId, CancellationToken ct)
    {
        var existing = await gallery.FindAsync(g => g.ExperienceId == experienceId, ct);
        return existing.Count == 0 ? 1 : existing.Max(g => g.SortOrder) + 1;
    }

    private Task LogAsync(string action, string entityType, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
