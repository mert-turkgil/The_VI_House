using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Seminars;
using VIHouse.Entities.Settings;

namespace VIHouse.Business.Concrete;

/// <summary>
/// The one place that knows how the site describes itself.
///
/// Cached deliberately: the layout reads this on every single page render, including the ones a
/// crawler hits in bursts, and a database round trip per page to fetch a dozen strings that change
/// once a month would be the most-executed query in the application.
/// </summary>
public class SiteSettingsService(
    ISiteSettingRepository repository,
    IRepository<SiteSettingTranslation> translations,
    IMediaStorage mediaStorage,
    IMemoryCache cache,
    IOptions<SiteOptions> siteOptions,
    IAuditLogRepository auditLogs) : ISiteSettingsService
{
    private const string CacheKey = "site-settings";
    private const string StorageFolder = "site";

    public async Task<SiteSetting> GetAsync(CancellationToken ct = default)
    {
        var existing = await repository.GetWithTranslationsAsync(ct);
        if (existing is not null) return existing;

        // First run on a fresh database. Seeded from the config that already exists rather than
        // left blank, so the site has a real title and a real canonical host before anyone opens
        // the admin screen — an empty title on day one is worse than an approximate one.
        var created = new SiteSetting
        {
            CanonicalBaseUrl = siteOptions.Value.BaseUrl.TrimEnd('/'),
            ContactEmail = siteOptions.Value.ContactEmail,
            Translations = [.. SiteCultures.All.Select(c => new SiteSettingTranslation
            {
                Culture = c.Name,
                SiteName = "The VI House",
                TitleTemplate = "{0} — The VI House",
            })],
        };

        await repository.AddAsync(created, ct);
        await repository.SaveChangesAsync(ct);

        return created;
    }

    public async Task<SiteSetting> GetCachedAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out SiteSetting? cached) && cached is not null) return cached;

        var loaded = await GetAsync(ct);

        // No expiry: every write path here evicts. A time-based window would mean an admin saving a
        // description and then not seeing it for five minutes, which reads as "the save did not
        // work" and produces a second save.
        cache.Set(CacheKey, loaded);
        return loaded;
    }

    public async Task UpdateAsync(SiteSetting updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var current = await GetAsync(ct);
        var before = Snapshot(current);

        current.CanonicalBaseUrl = Trim(updated.CanonicalBaseUrl)?.TrimEnd('/');
        current.OrganizationType = string.IsNullOrWhiteSpace(updated.OrganizationType)
            ? "Organization" : updated.OrganizationType.Trim();
        current.LegalName = Trim(updated.LegalName);
        current.FoundingDate = Trim(updated.FoundingDate);
        current.LogoUrl = Trim(updated.LogoUrl);
        current.DefaultOgImageUrl = Trim(updated.DefaultOgImageUrl);

        current.InstagramUrl = Trim(updated.InstagramUrl);
        current.LinkedInUrl = Trim(updated.LinkedInUrl);
        current.XUrl = Trim(updated.XUrl);
        current.FacebookUrl = Trim(updated.FacebookUrl);
        current.YouTubeUrl = Trim(updated.YouTubeUrl);
        current.TikTokUrl = Trim(updated.TikTokUrl);
        current.TwitterHandle = Trim(updated.TwitterHandle)?.TrimStart('@');

        current.ContactEmail = Trim(updated.ContactEmail);
        current.ContactPhone = Trim(updated.ContactPhone);
        current.StreetAddress = Trim(updated.StreetAddress);
        current.AddressLocality = Trim(updated.AddressLocality);
        current.AddressRegion = Trim(updated.AddressRegion);
        current.PostalCode = Trim(updated.PostalCode);
        current.AddressCountry = Trim(updated.AddressCountry)?.ToUpperInvariant();

        // Pasting the whole meta tag instead of the token is the most common mistake on this
        // screen, so the token is extracted rather than the value refused.
        current.GoogleSiteVerification = ExtractVerificationToken(updated.GoogleSiteVerification);
        current.BingSiteVerification = ExtractVerificationToken(updated.BingSiteVerification);

        current.AllowIndexing = updated.AllowIndexing;
        current.RobotsExtra = Trim(updated.RobotsExtra);
        current.PublishLlmsTxt = updated.PublishLlmsTxt;

        current.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SiteSettingsUpdated", current.Id, adminUserId, ipAddress, before, Snapshot(current), ct);
        await repository.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task<string?> SaveTranslationAsync(
        SiteSettingTranslation form, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (!SiteCultures.IsSupported(form.Culture))
            return "Admin.Settings.UnknownCulture";

        // The template assembles every title on the site. One without the placeholder silently
        // drops the page's own name from every tab and every search result, so it is refused here
        // rather than discovered weeks later in Search Console.
        if (!string.IsNullOrWhiteSpace(form.TitleTemplate) && !form.TitleTemplate.Contains("{0}"))
            return "Admin.Settings.TemplateNeedsPlaceholder";

        var current = await GetAsync(ct);
        var culture = SiteCultures.Normalise(form.Culture);
        var existing = current.Translations.FirstOrDefault(t => t.Culture == culture);

        if (existing is null)
        {
            existing = new SiteSettingTranslation { SiteSettingId = current.Id, Culture = culture };
            await translations.AddAsync(existing, ct);
        }

        existing.SiteName = string.IsNullOrWhiteSpace(form.SiteName) ? "The VI House" : form.SiteName.Trim();
        existing.TitleTemplate = string.IsNullOrWhiteSpace(form.TitleTemplate) ? "{0}" : form.TitleTemplate.Trim();
        existing.HomeTitle = Trim(form.HomeTitle);
        existing.DefaultMetaDescription = Trim(form.DefaultMetaDescription);
        existing.OrganizationDescription = Trim(form.OrganizationDescription);
        existing.OgImageAlt = Trim(form.OgImageAlt);
        existing.OgImageUrl = Trim(form.OgImageUrl);
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SiteSettingsTranslationSaved", current.Id, adminUserId, ipAddress,
            before: null, after: new { culture, existing.SiteName }, ct);

        await repository.SaveChangesAsync(ct);
        Invalidate();
        return null;
    }

    // --- Images -------------------------------------------------------------------------------------
    // Same ordering as every other upload in the codebase: classify, save the bytes, commit the row,
    // and only then delete what it replaced. See ExperienceService.UploadCoverAsync.

    public async Task<string?> UploadOgImageAsync(
        MediaUpload upload, string? culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (MediaPolicy.Classify(upload.FileName) is not SeminarMediaKind.Image)
            return "Admin.Settings.ImageTypeOnly";

        if (culture is not null && !SiteCultures.IsSupported(culture))
            return "Admin.Settings.UnknownCulture";

        var current = await GetAsync(ct);
        var saved = await mediaStorage.SaveAsync(upload, StorageFolder, ct);
        if (!saved.Success) return saved.Error;

        string? previous;

        if (culture is null)
        {
            previous = current.DefaultOgImageStorageKey;
            current.DefaultOgImageStorageKey = saved.StorageKey;
            current.DefaultOgImageUrl = null;
        }
        else
        {
            var translation = await EnsureTranslationAsync(current, SiteCultures.Normalise(culture), ct);
            previous = translation.OgImageStorageKey;
            translation.OgImageStorageKey = saved.StorageKey;
            translation.OgImageUrl = null;
        }

        current.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SiteSettingsOgImageUploaded", current.Id, adminUserId, ipAddress,
            before: new { Previous = previous, culture }, after: new { saved.StorageKey, culture }, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch
        {
            // The bytes are on disk and the row did not change, so the file belongs to nothing.
            await mediaStorage.DeleteAsync(saved.StorageKey!, ct);
            throw;
        }

        // Only after the commit: deleting first would lose the old image if the save then failed.
        if (previous is not null) await mediaStorage.DeleteAsync(previous, ct);

        Invalidate();
        return null;
    }

    public async Task RemoveOgImageAsync(string? culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var current = await GetAsync(ct);
        string? key;

        if (culture is null)
        {
            key = current.DefaultOgImageStorageKey;
            current.DefaultOgImageStorageKey = null;
        }
        else
        {
            var translation = current.Translations.FirstOrDefault(t => t.Culture == SiteCultures.Normalise(culture));
            key = translation?.OgImageStorageKey;
            if (translation is not null) translation.OgImageStorageKey = null;
        }

        if (key is null) return;

        current.UpdatedAt = DateTimeOffset.UtcNow;
        await LogAsync("SiteSettingsOgImageRemoved", current.Id, adminUserId, ipAddress,
            before: new { StorageKey = key, culture }, after: null, ct);

        await repository.SaveChangesAsync(ct);
        await mediaStorage.DeleteAsync(key, ct);
        Invalidate();
    }

    public async Task<string?> UploadLogoAsync(MediaUpload upload, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (MediaPolicy.Classify(upload.FileName) is not SeminarMediaKind.Image)
            return "Admin.Settings.ImageTypeOnly";

        var current = await GetAsync(ct);
        var saved = await mediaStorage.SaveAsync(upload, StorageFolder, ct);
        if (!saved.Success) return saved.Error;

        var previous = current.LogoStorageKey;
        current.LogoStorageKey = saved.StorageKey;
        current.LogoUrl = null;
        current.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SiteSettingsLogoUploaded", current.Id, adminUserId, ipAddress,
            before: new { Previous = previous }, after: new { saved.StorageKey }, ct);

        try
        {
            await repository.SaveChangesAsync(ct);
        }
        catch
        {
            await mediaStorage.DeleteAsync(saved.StorageKey!, ct);
            throw;
        }

        if (previous is not null) await mediaStorage.DeleteAsync(previous, ct);

        Invalidate();
        return null;
    }

    public async Task RemoveLogoAsync(Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var current = await GetAsync(ct);
        var key = current.LogoStorageKey;
        if (key is null) return;

        current.LogoStorageKey = null;
        current.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("SiteSettingsLogoRemoved", current.Id, adminUserId, ipAddress,
            before: new { StorageKey = key }, after: null, ct);

        await repository.SaveChangesAsync(ct);
        await mediaStorage.DeleteAsync(key, ct);
        Invalidate();
    }

    // --- Helpers ------------------------------------------------------------------------------------

    private async Task<SiteSettingTranslation> EnsureTranslationAsync(
        SiteSetting current, string culture, CancellationToken ct)
    {
        var existing = current.Translations.FirstOrDefault(t => t.Culture == culture);
        if (existing is not null) return existing;

        var created = new SiteSettingTranslation
        {
            SiteSettingId = current.Id,
            Culture = culture,
            SiteName = "The VI House",
            TitleTemplate = "{0} — The VI House",
        };

        await translations.AddAsync(created, ct);
        current.Translations.Add(created);
        return created;
    }

    private void Invalidate() => cache.Remove(CacheKey);

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Accepts either the bare token or the whole meta tag the search console hands you, and stores
    /// the token. Rendering a nested tag would produce invalid HTML and silently fail verification,
    /// with nothing on screen to explain why.
    /// </summary>
    private static string? ExtractVerificationToken(string? value)
    {
        var trimmed = Trim(value);
        if (trimmed is null) return null;
        if (!trimmed.Contains('<')) return trimmed;

        var match = Regex.Match(trimmed, "content=[\"']([^\"']+)[\"']", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : trimmed;
    }

    private static object Snapshot(SiteSetting s) => new
    {
        s.CanonicalBaseUrl, s.AllowIndexing, s.PublishLlmsTxt, s.TwitterHandle,
        s.OrganizationType, s.ContactEmail,
    };

    private Task LogAsync(
        string action, Guid id, Guid adminUserId, string? ipAddress,
        object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(SiteSetting),
            EntityId = id,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
