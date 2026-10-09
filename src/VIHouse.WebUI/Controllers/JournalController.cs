using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Journal;
using VIHouse.WebUI.ViewModels.Journal;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.Filters;

namespace VIHouse.WebUI.Controllers;

[Route("journal")]
public class JournalController(IJournalService journalService, IAmbassadorService ambassadors, IStringLocalizer<SharedResource> loc) : Controller
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

        // Staff can preview a draft (the admin's Preview link used to 404 on one), and an influencer
        // their own article; everyone else sees published posts only. A preview is never indexed.
        var viewerIsStaff = Roles.AdminRoles.Any(User.IsInRole);
        var viewerIsAuthor = post?.AuthorUserId is { } authorId && User.UserId() == authorId;
        if (post is null || (post.Status != JournalPostStatus.Published && !viewerIsStaff && !viewerIsAuthor))
            return NotFound();

        if (post.Status != JournalPostStatus.Published)
        {
            HttpContext.Items[NoIndexFilter.ItemKey] = true;
            ViewData["PreviewBanner"] = loc[viewerIsStaff ? "Preview.Journal.Draft" : "Preview.Journal.Author"].Value;
            ViewData["PreviewBannerEdit"] = viewerIsStaff
                ? Url.Action("Edit", "AdminJournal", new { area = "Admin", id = post.Id })
                : Url.Action("Write", "Influencer", new { id = post.Id });
        }

        ViewData["Seo"] = PageSeoBuilder.ForJournalPost(
            post, CultureInfo.CurrentUICulture.Name, loc["Journal.Heading"].Value);

        var model = JournalPostDetailViewModel.FromEntity(
            post, CultureInfo.CurrentUICulture.Name, loc["Journal.Video.Play"].Value, loc["Journal.LinkCard.ViewOn"].Value);
        model.Author = await AuthorBoxAsync(post, ct);
        return View(model);
    }

    /// <summary>
    /// "About the author" under an influencer's article: photo, bio, channels, and a way to join
    /// through their link — so a reader who signs up counts toward the influencer's earnings. The
    /// join button is only offered while their links are switched on.
    /// </summary>
    private async Task<JournalAuthorBox?> AuthorBoxAsync(JournalPost post, CancellationToken ct)
    {
        if (post.AuthorUserId is not { } authorId) return null;
        var influencer = await ambassadors.GetByUserIdAsync(authorId, ct);
        if (influencer is null) return null;

        return new JournalAuthorBox(
            influencer.Name,
            influencer.PhotoStorageKey is { } key ? SiteUrls.InfluencerPhoto(influencer.Id, key) : null,
            influencer.Bio,
            influencer.Niche,
            influencer.Channels,
            influencer.Status == Entities.Referrals.AmbassadorStatus.Active ? SiteUrls.Referral(influencer.Code) : null);
    }
}
