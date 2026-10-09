using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Content;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Settings;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Serves public files that were uploaded through the admin panel rather than committed to
/// wwwroot — hero slide photography, experience covers and galleries, and the images, GIFs and
/// audio inside journal articles.
///
/// Uploads cannot simply be written into wwwroot: it is served by MapStaticAssets, which only knows
/// about files that existed at build time, so a runtime upload there works in Development and 404s
/// in Production. They go to the media root instead (outside wwwroot, see LocalMediaStorage) and
/// come back out through here.
///
/// No access check for the site's own imagery, unlike SeminarsController.Media — a hero slide is the
/// first thing an anonymous visitor sees, so its photograph is public by definition. The exceptions
/// are files whose owner has not gone public yet: an unpublished article's media, and an
/// influencer's photo before their first published article. The storage key is never taken from the
/// request: it is read from the slide row, which is what stops this being an arbitrary-file reader.
///
/// Note the absence of VaryByQueryKeys on the version-stamped routes. It is a *server-side*
/// response-cache directive and throws at runtime unless UseResponseCaching is in the pipeline,
/// which it is not — so the hero route had been returning a 500 since it was written, latent only
/// because nobody had uploaded a hero image to trigger it. It buys nothing here either: the stamp is
/// part of the URL, so a replaced image is already a different URL to a browser and to a CDN, and
/// Cache-Control is what does the work.
/// </summary>
[Route("media")]
public class MediaController(
    IHeroSlideRepository heroSlides,
    IJournalService journalService,
    IAmbassadorService ambassadors,
    IRepository<MediaAsset> assets,
    IExperienceRepository experiences,
    IRepository<ExperienceImage> galleryImages,
    ISiteSettingRepository siteSettings,
    IRepository<SiteSettingTranslation> siteSettingTranslations,
    IMediaStorage mediaStorage) : Controller
{
    /// <summary>
    /// The site-wide social image — the picture that appears when any link to this site is pasted
    /// into a chat. Fetched by crawlers and by every link-preview bot, so it is cached hard.
    /// </summary>
    [HttpGet("site-og-default/{id:guid}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SiteOgDefault(Guid id, CancellationToken ct)
    {
        var settings = await siteSettings.GetByIdAsync(id, ct);
        return await ServeAsync(settings?.DefaultOgImageStorageKey, ct);
    }

    /// <summary>The organisation logo, referenced from the schema.org graph.</summary>
    [HttpGet("site-logo/{id:guid}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SiteLogo(Guid id, CancellationToken ct)
    {
        var settings = await siteSettings.GetByIdAsync(id, ct);
        return await ServeAsync(settings?.LogoStorageKey, ct);
    }

    /// <summary>
    /// One language's own social image, addressed by the translation row's id. Exists because a
    /// card with English words baked into the picture under a Turkish headline reads as a mistake.
    /// </summary>
    [HttpGet("site-og/{id:guid}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> SiteOgForCulture(Guid id, CancellationToken ct)
    {
        var translation = await siteSettingTranslations.GetByIdAsync(id, ct);
        return await ServeAsync(translation?.OgImageStorageKey, ct);
    }

    /// <summary>The key always comes from a database row, never from the request — see the note above.</summary>
    private async Task<IActionResult> ServeAsync(string? storageKey, CancellationToken ct)
    {
        if (storageKey is null) return NotFound();

        var file = await mediaStorage.GetAsync(storageKey, ct);
        return file is null ? NotFound() : PhysicalFile(file.PhysicalPath, file.ContentType);
    }

    /// <summary>
    /// An experience's cover photograph, when it was uploaded rather than typed as a path.
    ///
    /// Version-stamped like the hero slide's, so replacing the cover produces a new URL rather than
    /// waiting for a cached one to expire — hence the same week-long cache.
    /// </summary>
    [HttpGet("experience/{id:guid}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> ExperienceCover(Guid id, CancellationToken ct)
    {
        var experience = await experiences.GetByIdAsync(id, ct);
        if (experience?.CoverImageStorageKey is null) return NotFound();

        var file = await mediaStorage.GetAsync(experience.CoverImageStorageKey, ct);
        if (file is null) return NotFound();

        return PhysicalFile(file.PhysicalPath, file.ContentType);
    }

    /// <summary>
    /// One photograph from an experience's gallery. Addressed by the image row's own id, so the key
    /// still comes from the database rather than the request.
    /// </summary>
    [HttpGet("experience-gallery/{id:guid}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> ExperienceGalleryImage(Guid id, CancellationToken ct)
    {
        var image = await galleryImages.GetByIdAsync(id, ct);
        if (image?.StorageKey is null) return NotFound();

        var file = await mediaStorage.GetAsync(image.StorageKey, ct);
        if (file is null) return NotFound();

        return PhysicalFile(file.PhysicalPath, file.ContentType);
    }

    /// <summary>
    /// Cached hard, because the URL carries a version stamp (see HomeController.HeroImageUrl) and a
    /// replaced image therefore arrives on a new URL rather than needing the old one to expire.
    /// </summary>
    [HttpGet("hero/{id:guid}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> HeroImage(Guid id, CancellationToken ct)
    {
        var slide = await heroSlides.GetByIdAsync(id, ct);
        if (slide?.ImageStorageKey is null) return NotFound();

        var file = await mediaStorage.GetAsync(slide.ImageStorageKey, ct);
        if (file is null) return NotFound();

        return PhysicalFile(file.PhysicalPath, file.ContentType);
    }

    /// <summary>
    /// An unowned asset — the images admins upload for homepage content. Cached for a day, like
    /// journal media: the URL carries no version stamp, and an asset that is replaced is a new
    /// upload with a new id rather than the same id with new bytes.
    /// </summary>
    [HttpGet("asset/{id:guid}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Asset(Guid id, CancellationToken ct)
    {
        var asset = await assets.GetByIdAsync(id, ct);
        if (asset is null) return NotFound();

        var file = await mediaStorage.GetAsync(asset.StorageKey, ct);
        if (file is null) return NotFound();

        return PhysicalFile(file.PhysicalPath, file.ContentType);
    }

    /// <summary>
    /// An asset belonging to a journal article — the URL written into the body by the editor.
    ///
    /// Addressed by media id rather than by article slug so it survives the post being renamed, and
    /// range-enabled because audio is served through here: without it a browser cannot seek in a
    /// track, it can only play from the beginning.
    ///
    /// Public, and cached for a day, once the article is published (the URL is not version-stamped).
    /// Before that — a draft, or an influencer's submission — only the staff and the article's
    /// author get it, and nothing along the way may keep a copy.
    /// </summary>
    [HttpGet("journal/{mediaId:guid}")]
    public async Task<IActionResult> JournalMedia(Guid mediaId, CancellationToken ct)
    {
        var media = await journalService.OpenMediaAsync(mediaId, ct);
        if (media is null) return NotFound();

        if (media.IsPublic) CachePublicly(TimeSpan.FromDays(1));
        else if (IsStaffOr(media.AuthorUserId)) KeepPrivate();
        else return NotFound();

        return PhysicalFile(media.File.PhysicalPath, media.File.ContentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// An influencer's profile photo. Public once they have a published article — it is in the
    /// author box under it. Until then only the staff and the influencer see it. The links carry a
    /// version stamp (see SiteUrls.InfluencerPhoto), so a new photo is a new URL.
    /// </summary>
    [HttpGet("influencer/{id:guid}")]
    public async Task<IActionResult> InfluencerPhoto(Guid id, CancellationToken ct)
    {
        var influencer = await ambassadors.GetByIdAsync(id, ct);
        if (influencer?.PhotoStorageKey is null) return NotFound();

        if (influencer.UserId is { } userId && await journalService.HasPublishedAsync(userId, ct)) CachePublicly(TimeSpan.FromDays(7));
        else if (IsStaffOr(influencer.UserId)) KeepPrivate();
        else return NotFound();

        var file = await ambassadors.OpenPhotoAsync(id, ct);
        return file is null ? NotFound() : PhysicalFile(file.PhysicalPath, file.ContentType);
    }

    private bool IsStaffOr(Guid? ownerUserId) =>
        Roles.AdminRoles.Any(User.IsInRole) || (ownerUserId is not null && User.UserId() == ownerUserId);

    private void CachePublicly(TimeSpan maxAge) =>
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = maxAge };

    private void KeepPrivate() =>
        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue { Private = true, NoStore = true };
}
