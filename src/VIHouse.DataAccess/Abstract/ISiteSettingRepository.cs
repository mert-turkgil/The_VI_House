using VIHouse.Entities.Settings;

namespace VIHouse.DataAccess.Abstract;

public interface ISiteSettingRepository : IRepository<SiteSetting>
{
    /// <summary>
    /// The single settings row with its translations attached. Null only before the first save,
    /// which SiteSettingsService turns into a seeded row rather than a null the layout has to
    /// defend against on every render.
    /// </summary>
    Task<SiteSetting?> GetWithTranslationsAsync(CancellationToken ct = default);
}
