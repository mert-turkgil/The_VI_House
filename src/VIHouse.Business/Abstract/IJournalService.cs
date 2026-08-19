using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Journal;

namespace VIHouse.Business.Abstract;

public interface IJournalService
{
    // --- Public ---
    Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default);
    Task<JournalPost?> GetPublicDetailBySlugAsync(string slug, CancellationToken ct = default);

    // --- Admin --- (every mutation is audit-logged)
    Task<List<JournalPost>> GetAllForAdminAsync(CancellationToken ct = default);
    Task<JournalPost?> GetForAdminEditAsync(Guid id, CancellationToken ct = default);
    Task<JournalPost> CreateAsync(JournalPost post, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task UpdateAsync(JournalPost updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}
