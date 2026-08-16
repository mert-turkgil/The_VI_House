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
        ExperienceStatus? parsedStatus = Enum.TryParse<ExperienceStatus>(status, out var s) ? s : null;
        var filter = new ExperienceFilter { City = city, Status = parsedStatus, Take = 50 };
        var experiences = await experienceService.GetPublicListingAsync(filter, ct);

        var model = experiences.Select(ExperienceCardViewModel.FromEntity).ToList();
        return View(model);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var experience = await experienceService.GetPublicDetailBySlugAsync(slug, ct);
        if (experience is null || experience.Visibility != ExperienceVisibility.Public)
            return NotFound();

        return View(ExperienceDetailViewModel.FromEntity(experience));
    }
}
