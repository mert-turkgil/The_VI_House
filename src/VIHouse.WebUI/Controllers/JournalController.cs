using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.ViewModels.Journal;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.Filters;

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
        this.SetSeo(loc["Seo.Journal.Description"].Value, canonicalPath: "/journal", pageKey: "journal");
        return View(model);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken ct)
    {
        var post = await journalService.GetPublicDetailBySlugAsync(slug, ct);

        // Staff can preview a draft (the admin's Preview link used to 404 on one); everyone else
        // sees published posts only. A preview is never indexed.
        var viewerIsStaff = Roles.AdminRoles.Any(User.IsInRole);
        if (post is null || (post.Status != JournalPostStatus.Published && !viewerIsStaff))
            return NotFound();

        if (post.Status != JournalPostStatus.Published)
        {
            HttpContext.Items[NoIndexFilter.ItemKey] = true;
            ViewData["PreviewBanner"] = loc["Preview.Journal.Draft"].Value;
            ViewData["PreviewBannerEdit"] = Url.Action("Edit", "AdminJournal", new { area = "Admin", id = post.Id });
        }

        ViewData["Seo"] = PageSeoBuilder.ForJournalPost(
            post, CultureInfo.CurrentUICulture.Name, loc["Journal.Heading"].Value);

        return View(JournalPostDetailViewModel.FromEntity(
            post, CultureInfo.CurrentUICulture.Name, loc["Journal.Video.Play"].Value));
    }
}
