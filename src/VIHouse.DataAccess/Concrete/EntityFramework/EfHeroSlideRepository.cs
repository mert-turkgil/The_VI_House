using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfHeroSlideRepository(VIHouseDbContext db) : EfRepository<HeroSlide>(db), IHeroSlideRepository
{
    public async Task<List<HeroSlide>> GetAllWithTranslationsAsync(CancellationToken ct = default) =>
        await Set.Include(s => s.Translations)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<HeroSlide>> GetVisibleAsync(DateTimeOffset asOfUtc, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(s => s.Translations)
            .Where(s => s.IsActive
                && (s.VisibleFromUtc == null || s.VisibleFromUtc <= asOfUtc)
                && (s.VisibleUntilUtc == null || s.VisibleUntilUtc >= asOfUtc))
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task<HeroSlide?> GetByIdWithTranslationsAsync(Guid id, CancellationToken ct = default) =>
        await Set.Include(s => s.Translations).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<int> GetNextSortOrderAsync(CancellationToken ct = default) =>
        await Set.AnyAsync(ct) ? await Set.MaxAsync(s => s.SortOrder, ct) + 1 : 0;
}
