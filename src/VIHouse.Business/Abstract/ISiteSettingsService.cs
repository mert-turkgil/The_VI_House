using VIHouse.Entities.Settings;

namespace VIHouse.Business.Abstract;

public interface ISiteSettingsService
{
    /// <summary>
    /// The settings row, with translations, creating a sensible default one on first call so the
    /// admin screen and the layout both have something real to work with on a fresh database.
    /// </summary>
    Task<SiteSetting> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Cached for the layout, which reads this on literally every page render. Invalidated by any
    /// save — a settings screen where the change does not show up until a restart is a settings
    /// screen nobody trusts.
    /// </summary>
    Task<SiteSetting> GetCachedAsync(CancellationToken ct = default);

    Task UpdateAsync(SiteSetting updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Writes or updates one language's copy. Returns a resource key on failure.</summary>
    Task<string?> SaveTranslationAsync(SiteSettingTranslation form, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    Task<string?> UploadOgImageAsync(MediaUpload upload, string? culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveOgImageAsync(string? culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task<string?> UploadLogoAsync(MediaUpload upload, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveLogoAsync(Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}
