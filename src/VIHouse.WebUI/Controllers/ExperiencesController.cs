using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Experiences;
using VIHouse.WebUI.ViewModels.Experiences;

namespace VIHouse.WebUI.Controllers;

[Route("experiences")]
public class ExperiencesController(IExperienceService experienceService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? city, string? status, CancellationToken ct)
    {
        ViewData["Title"] = "Experiences";
        return View(await BuildIndexAsync(city, status, ct));
    }

    /// <summary>
    /// The results region alone, for the filter enhancement in modules/filters.ts.
    ///
    /// Returns the same partial Index renders rather than JSON, so there is exactly one place that
    /// knows what a card looks like. A JSON endpoint would mean rebuilding the markup in TypeScript
    /// and keeping two renderers in step — which is how the enhanced and unenhanced views of a page
    /// start telling visitors different things.
    /// </summary>
    [HttpGet("results")]
    public async Task<IActionResult> Results(string? city, string? status, CancellationToken ct) =>
        PartialView("_ExperienceGrid", await BuildIndexAsync(city, status, ct));

    private async Task<ExperienceIndexViewModel> BuildIndexAsync(string? city, string? status, CancellationToken ct)
    {
        // Unparseable status is treated as "no filter" rather than an error: this arrives from a
        // query string, and a stale or hand-edited link should show the unfiltered page, not a 400.
        ExperienceStatus? parsedStatus = Enum.TryParse<ExperienceStatus>(status, out var s) ? s : null;

        var filter = new ExperienceFilter { City = city, Status = parsedStatus, Take = 50 };
        var experiences = await experienceService.GetPublicListingAsync(filter, ct);

        return new ExperienceIndexViewModel
        {
            Cards = experiences.Select(ExperienceCardViewModel.FromEntity).ToList(),
            Cities = await experienceService.GetPublicCitiesAsync(ct),
            SelectedCity = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            SelectedStatus = parsedStatus,
        };
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var experience = await experienceService.GetPublicDetailBySlugAsync(slug, ct);
        if (experience is null || experience.Visibility != ExperienceVisibility.Public || experience.Status == ExperienceStatus.Draft)
            return NotFound();

        return View(ExperienceDetailViewModel.FromEntity(experience));
    }
}
