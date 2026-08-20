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

    /// <summary>Permanently removes a post. Returns false when the id no longer exists (e.g. a
    /// double-submitted delete), so the caller can report that without treating it as an error.</summary>
    Task<bool> DeleteAsync(Guid id, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}
