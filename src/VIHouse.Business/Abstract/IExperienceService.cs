using VIHouse.DataAccess.Abstract;
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
}
