using VIHouse.Entities.Journal;

namespace VIHouse.DataAccess.Abstract;

/// <summary>
/// Journal posts always travel with their translations — there is no screen that wants a post
/// without knowing what it says — so every read here includes them rather than leaving callers to
/// remember an Include and discover the omission as a blank headline.
/// </summary>
public interface IJournalPostRepository : IRepository<JournalPost>
{
    Task<JournalPost?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>True when another post (not <paramref name="exceptId"/>) already uses the slug. The
    /// column has a unique index, so this is what turns a clash into a message instead of a 500.</summary>
    Task<bool> SlugExistsAsync(string slug, Guid? exceptId = null, CancellationToken ct = default);

    Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default);

    /// <summary>Everything the admin index needs, newest first.</summary>
    Task<List<JournalPost>> GetAllWithTranslationsAsync(CancellationToken ct = default);

    /// <summary>One author's posts with their copy, newest first — the influencer's journal list.</summary>
    Task<List<JournalPost>> GetByAuthorAsync(Guid authorUserId, CancellationToken ct = default);

    /// <summary>The full graph — copy and files — for the admin editor and for every mutation that
    /// has to reason about both, which is all of them once media is involved.</summary>
    Task<JournalPost?> GetWithDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>One media row, for the streaming endpoint (which then asks who may see its post).</summary>
    Task<JournalPostMedia?> GetMediaAsync(Guid mediaId, CancellationToken ct = default);

    /// <summary>
    /// Posts whose copy matches <paramref name="term"/> in any language, for site search. Matching
    /// across every translation rather than only the reader's own is deliberate: someone searching
    /// a German phrase should find the article even while reading the site in English.
    /// </summary>
    Task<List<JournalPost>> SearchPublishedAsync(string term, CancellationToken ct = default);
}

/// <summary>Filter set for the public Journal listing (category filter from the querystring).</summary>
public record JournalPostFilter
{
    public JournalCategory? Category { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 20;
}
