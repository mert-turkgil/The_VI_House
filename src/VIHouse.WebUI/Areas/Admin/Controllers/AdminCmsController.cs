using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Content;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>Admin CRUD over ContentPage/ContentBlock (the "CMS" bullet in brief §206) — currently just
/// the homepage's sections. English-only: CMS content stays outside this pass's 4-language scope
/// (see Program.cs comment), so this screen doesn't need a locale picker.</summary>
public class AdminCmsController(IContentService contentService, UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pages = await contentService.GetAllPagesAsync(ct);
        return View(pages);
    }

    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var page = await contentService.GetPageWithBlocksAsync(id, ct);
        if (page is null) return NotFound();

        page.Blocks = page.Blocks.OrderBy(b => b.SortOrder).ToList();
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBlock(AdminContentBlockFormModel form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (adminId, ip) = CurrentActor();
            await contentService.UpdateBlockAsync(new ContentBlock
            {
                Id = form.Id,
                SectionKey = form.SectionKey,
                SortOrder = form.SortOrder,
                Heading = form.Heading,
                Subheading = form.Subheading,
                BodyText = form.BodyText,
                ImageUrl = form.ImageUrl,
                CtaLabel = form.CtaLabel,
                CtaUrl = form.CtaUrl,
                ExtraJson = form.ExtraJson,
            }, adminId, ip, ct);
            TempData["StatusMessage"] = "Block saved.";
        }

        return RedirectToAction(nameof(Edit), new { id = form.PageSlug });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBlock(AdminContentBlockFormModel form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (adminId, ip) = CurrentActor();
            await contentService.AddBlockAsync(form.PageId, new ContentBlock
            {
                SectionKey = form.SectionKey,
                SortOrder = form.SortOrder,
                Heading = form.Heading,
                Subheading = form.Subheading,
                BodyText = form.BodyText,
                ImageUrl = form.ImageUrl,
                CtaLabel = form.CtaLabel,
                CtaUrl = form.CtaUrl,
                ExtraJson = form.ExtraJson,
            }, adminId, ip, ct);
            TempData["StatusMessage"] = "Block added.";
        }

        return RedirectToAction(nameof(Edit), new { id = form.PageSlug });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBlock(Guid pageId, Guid blockId, string pageSlug, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await contentService.RemoveBlockAsync(pageId, blockId, adminId, ip, ct);
        TempData["StatusMessage"] = "Block removed.";
        return RedirectToAction(nameof(Edit), new { id = pageSlug });
    }

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
