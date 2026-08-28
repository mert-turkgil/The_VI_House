using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// Serves public images that were uploaded through the admin panel rather than committed to
/// wwwroot — today, hero slide photography.
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
public class MediaController(IHeroSlideRepository heroSlides, IMediaStorage mediaStorage) : Controller
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
}
