using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Settings;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfSiteSettingRepository(VIHouseDbContext db)
    : EfRepository<SiteSetting>(db), ISiteSettingRepository
{
    public Task<SiteSetting?> GetWithTranslationsAsync(CancellationToken ct = default) =>
        Set.Include(s => s.Translations)
            // Ordered so that a table which somehow grew a second row still resolves to the same
            // one every time rather than alternating between them.
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
}
