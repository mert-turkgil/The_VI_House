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

    // --- Admin ---
    Task<List<Experience>> GetAllForAdminAsync(CancellationToken ct = default);
    Task<Experience?> GetForAdminEditAsync(Guid id, CancellationToken ct = default);
    Task<Experience> CreateAsync(Experience experience, CancellationToken ct = default);
    Task UpdateCoreFieldsAsync(Experience updated, CancellationToken ct = default);

    /// <summary>False (rather than throwing) if the experience can't be deleted — e.g. it already has applications/bookings against it.</summary>
    Task<bool> TryDeleteAsync(Guid id, CancellationToken ct = default);

    Task AddTicketTypeAsync(Guid experienceId, TicketType ticketType, CancellationToken ct = default);
    Task<bool> TryRemoveTicketTypeAsync(Guid experienceId, Guid ticketTypeId, CancellationToken ct = default);

    Task AddInclusionAsync(Guid experienceId, ExperienceInclusion inclusion, CancellationToken ct = default);
    Task RemoveInclusionAsync(Guid experienceId, Guid inclusionId, CancellationToken ct = default);

    Task AddFaqAsync(Guid experienceId, ExperienceFaq faq, CancellationToken ct = default);
    Task RemoveFaqAsync(Guid experienceId, Guid faqId, CancellationToken ct = default);
}
