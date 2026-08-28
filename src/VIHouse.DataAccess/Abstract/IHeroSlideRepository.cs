using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Abstract;

/// <summary>
/// Hero slides always travel with their translations — there is no screen that wants a slide
/// without knowing what it says — so every read here includes them rather than leaving callers to
/// remember an Include and discover the omission as an empty heading.
/// </summary>
public interface IHeroSlideRepository : IRepository<HeroSlide>
{
    /// <summary>Everything, ordered for the admin index.</summary>
    Task<List<HeroSlide>> GetAllWithTranslationsAsync(CancellationToken ct = default);

    /// <summary>What the homepage shows: active, in schedule at <paramref name="asOfUtc"/>, in order.</summary>
    Task<List<HeroSlide>> GetVisibleAsync(DateTimeOffset asOfUtc, CancellationToken ct = default);

    Task<HeroSlide?> GetByIdWithTranslationsAsync(Guid id, CancellationToken ct = default);

    /// <summary>The next free position, so a newly created slide lands at the end rather than
    /// silently sharing position 0 with whatever is currently first.</summary>
    Task<int> GetNextSortOrderAsync(CancellationToken ct = default);
}
