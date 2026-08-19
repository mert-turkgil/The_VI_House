using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.ViewModels.Journal;

namespace VIHouse.WebUI.Controllers;

[Route("journal")]
public class JournalController(IJournalService journalService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? category, CancellationToken ct)
    {
        JournalCategory? parsedCategory = Enum.TryParse<JournalCategory>(category, out var c) ? c : null;
        var filter = new JournalPostFilter { Category = parsedCategory, Take = 50 };
        var posts = await journalService.GetPublicListingAsync(filter, ct);

        var model = posts.Select(JournalPostCardViewModel.FromEntity).ToList();
        return View(model);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var post = await journalService.GetPublicDetailBySlugAsync(slug, ct);
        if (post is null || post.Status != JournalPostStatus.Published)
            return NotFound();

        return View(JournalPostDetailViewModel.FromEntity(post));
    }
}
