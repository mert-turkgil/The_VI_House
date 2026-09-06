using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;

namespace VIHouse.Business.Abstract;

public interface IExperienceService
{
    // --- Public ---
    Task<List<Experience>> GetPublicListingAsync(ExperienceFilter filter, CancellationToken ct = default);
    Task<Experience?> GetPublicDetailBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<Experience>> GetUpcomingAsync(int take, CancellationToken ct = default);
    Task<List<Experience>> GetSignatureAsync(int take, CancellationToken ct = default);

    /// <summary>Cities that currently have something publicly listed — populates the listing
    /// page's city filter with real values instead of a free-text box.</summary>
    Task<List<string>> GetPublicCitiesAsync(CancellationToken ct = default);

    // --- Admin --- (adminUserId/ipAddress: every mutation here is audit-logged, brief §97)
    Task<List<Experience>> GetAllForAdminAsync(CancellationToken ct = default);
    Task<Experience?> GetForAdminEditAsync(Guid id, CancellationToken ct = default);
    Task<Experience> CreateAsync(Experience experience, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task UpdateCoreFieldsAsync(Experience updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>False (rather than throwing) if the experience can't be deleted — e.g. it already has applications/bookings against it.</summary>
    Task<bool> TryDeleteAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task AddTicketTypeAsync(Guid experienceId, TicketType ticketType, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task<bool> TryRemoveTicketTypeAsync(Guid experienceId, Guid ticketTypeId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task AddInclusionAsync(Guid experienceId, ExperienceInclusion inclusion, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveInclusionAsync(Guid experienceId, Guid inclusionId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task AddFaqAsync(Guid experienceId, ExperienceFaq faq, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveFaqAsync(Guid experienceId, Guid faqId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- Editing what add/remove alone could not reach ---------------------------------------------
    // Every one of these returns a resource key on failure and null on success. Business has no
    // IStringLocalizer, so the key is localised by the controller — the same arrangement
    // SeminarService uses.

    Task<string?> UpdateTicketTypeAsync(Guid experienceId, TicketType updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task<string?> UpdateInclusionAsync(Guid experienceId, Guid inclusionId, string text, bool isIncluded, int sortOrder, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task<string?> UpdateFaqAsync(Guid experienceId, Guid faqId, string question, string answer, int sortOrder, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- Programme ---------------------------------------------------------------------------------
    // Days and their sessions were readable on the public page and editable nowhere.

    Task AddProgramDayAsync(Guid experienceId, ExperienceProgramDay day, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveProgramDayAsync(Guid experienceId, Guid dayId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task<string?> AddSessionAsync(Guid experienceId, Guid dayId, ExperienceSession session, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveSessionAsync(Guid experienceId, Guid sessionId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- Images ------------------------------------------------------------------------------------
    // An upload always wins over a typed URL and nulls it, so exactly one of the pair is ever set.

    Task<string?> UploadCoverAsync(Guid experienceId, MediaUpload upload, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveCoverAsync(Guid experienceId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<string?> AddGalleryImageAsync(Guid experienceId, MediaUpload upload, string? altText, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task<string?> AddGalleryImageByUrlAsync(Guid experienceId, string url, string? altText, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task UpdateGalleryImageAsync(Guid experienceId, Guid imageId, string? altText, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveGalleryImageAsync(Guid experienceId, Guid imageId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Swaps this image with its neighbour. <paramref name="delta"/> is -1 for up, +1 for down.</summary>
    Task MoveGalleryImageAsync(Guid experienceId, Guid imageId, int delta, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    // --- Who may join, and how ------------------------------------------------------------------------

    /// <summary>The membership plans that may join this experience without applying.</summary>
    Task<List<Guid>> GetMembershipAccessAsync(Guid experienceId, CancellationToken ct = default);

    /// <summary>Replaces the whole set — the admin screen posts every ticked box at once.</summary>
    Task SetMembershipAccessAsync(Guid experienceId, IReadOnlyCollection<Guid> planIds, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// What this person can do about this experience right now, decided entirely from current
    /// database state. Nothing about entitlement is ever posted from a form — see
    /// SeminarService.GetAccessAsync, which this deliberately mirrors.
    /// </summary>
    Task<ExperienceAccessInfo> GetAccessAsync(Experience experience, Guid? userId, CancellationToken ct = default);

    /// <summary>
    /// Books a place for a member whose plan admits them. Re-derives the entitlement rather than
    /// trusting the caller, and returns a resource key if they are not in fact entitled.
    /// </summary>
    Task<(string? Error, string? BookingReference)> JoinAsMemberAsync(
        Guid experienceId, Guid userId, BookingAttendance? attendance, CancellationToken ct = default);

    // --- Translations ---------------------------------------------------------------------------------

    /// <summary>Writes or updates one language's copy. Returns a resource key on failure.</summary>
    Task<string?> SaveTranslationAsync(Guid experienceId, ExperienceTranslation form, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Removes one language's copy. The default culture cannot be removed.</summary>
    Task<string?> DeleteTranslationAsync(Guid experienceId, string culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}

/// <summary>
/// The rungs of the access ladder, in the order GetAccessAsync tests them.
/// </summary>
public enum ExperienceAccessOutcome
{
    /// <summary>They already hold a booking. Outranks everything below, including a closed experience.</summary>
    AlreadyBooked,

    /// <summary>Signed out, on an experience a membership could otherwise get them into.</summary>
    NeedsSignIn,

    /// <summary>An active membership on an admitted plan — one click, no form, no payment.</summary>
    IncludedInMembership,

    /// <summary>The ordinary route: fill in the form and wait to be accepted.</summary>
    RequiresApplication,

    /// <summary>Draft, closed, or already finished.</summary>
    NotOpen,
}

/// <param name="BookingReference">Set only when they already hold a booking, so the page can link to it.</param>
public record ExperienceAccessInfo(
    ExperienceAccessOutcome Outcome,
    string? BookingReference = null)
{
    /// <summary>Whether to render a join button at all.</summary>
    public bool CanJoinNow => Outcome == ExperienceAccessOutcome.IncludedInMembership;
}
