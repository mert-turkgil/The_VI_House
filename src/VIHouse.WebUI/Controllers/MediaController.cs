using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Serves public files that were uploaded through the admin panel rather than committed to
/// wwwroot — hero slide photography, and the images, GIFs and audio inside journal articles.
///
/// Uploads cannot simply be written into wwwroot: it is served by MapStaticAssets, which only knows
/// about files that existed at build time, so a runtime upload there works in Development and 404s
/// in Production. They go to the media root instead (outside wwwroot, see LocalMediaStorage) and
/// come back out through here.
///
/// No access check, unlike SeminarsController.Media — a hero slide is the first thing an anonymous
/// visitor sees, so its photograph is public by definition. The storage key is never taken from the
/// request: it is read from the slide row, which is what stops this being an arbitrary-file reader.
/// </summary>
[Route("media")]
public class MediaController(
    IHeroSlideRepository heroSlides,
    IJournalService journalService,
    IMediaStorage mediaStorage) : Controller
{
    /// <summary>
    /// Cached hard, because the URL carries a version stamp (see HomeController.HeroImageUrl) and a
    /// replaced image therefore arrives on a new URL rather than needing the old one to expire.
    /// </summary>
    [HttpGet("hero/{id:guid}")]
    [ResponseCache(Duration = 604800, Location = ResponseCacheLocation.Any, VaryByQueryKeys = ["v"])]
    public async Task<IActionResult> HeroImage(Guid id, CancellationToken ct)
    {
        var slide = await heroSlides.GetByIdAsync(id, ct);
        if (slide?.ImageStorageKey is null) return NotFound();

        var file = await mediaStorage.GetAsync(slide.ImageStorageKey, ct);
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
    /// Cached for a day rather than a week (the hero's version-stamped URLs can be cached hard;
    /// these are not stamped), and only for as long as the row exists — a deleted asset 404s
    /// immediately at the origin.
    /// </summary>
    [HttpGet("journal/{mediaId:guid}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> JournalMedia(Guid mediaId, CancellationToken ct)
    {
        var file = await journalService.OpenMediaAsync(mediaId, ct);
        if (file is null) return NotFound();

        return PhysicalFile(file.PhysicalPath, file.ContentType, enableRangeProcessing: true);
    }
}
