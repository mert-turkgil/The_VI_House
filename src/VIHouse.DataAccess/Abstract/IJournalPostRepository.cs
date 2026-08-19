using VIHouse.Entities.Journal;

namespace VIHouse.DataAccess.Abstract;

public interface IJournalPostRepository : IRepository<JournalPost>
{
    Task<JournalPost?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default);
}

/// <summary>Filter set for the public Journal listing (category filter from the querystring).</summary>
public record JournalPostFilter
{
    public JournalCategory? Category { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 20;
}
