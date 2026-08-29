using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.ViewModels.Journal;

namespace VIHouse.WebUI.Controllers;

[Route("journal")]
public class JournalController(IJournalService journalService, IStringLocalizer<SharedResource> loc) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? category, CancellationToken ct)
    {
        JournalCategory? parsedCategory = Enum.TryParse<JournalCategory>(category, out var c) ? c : null;
        var filter = new JournalPostFilter { Category = parsedCategory, Take = 50 };
        var posts = await journalService.GetPublicListingAsync(filter, ct);

        var culture = CultureInfo.CurrentUICulture.Name;
        var model = posts.Select(p => JournalPostCardViewModel.FromEntity(p, culture)).ToList();
        return View(model);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var post = await journalService.GetPublicDetailBySlugAsync(slug, ct);
        if (post is null || post.Status != JournalPostStatus.Published)
            return NotFound();

        return View(JournalPostDetailViewModel.FromEntity(
            post, CultureInfo.CurrentUICulture.Name, loc["Journal.Video.Play"].Value));
    }
}
