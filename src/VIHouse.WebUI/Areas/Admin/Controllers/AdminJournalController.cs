using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminJournalController(IJournalService journalService, UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var posts = await journalService.GetAllForAdminAsync(ct);
        return View(posts);
    }

    [HttpGet]
    public IActionResult Create() => View(new AdminJournalPostFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminJournalPostFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var entity = form.ToEntity();
        var (adminId, ip) = CurrentActor();
        await journalService.CreateAsync(entity, adminId, ip, ct);

        TempData["StatusMessage"] = $"\"{entity.Title}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var post = await journalService.GetForAdminEditAsync(id, ct);
        if (post is null) return NotFound();

        return View(AdminJournalPostFormViewModel.FromEntity(post));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AdminJournalPostFormViewModel form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid) return View(form);

        var (adminId, ip) = CurrentActor();
        await journalService.UpdateAsync(form.ToEntity(), adminId, ip, ct);

        TempData["StatusMessage"] = "Changes saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var deleted = await journalService.DeleteAsync(id, adminId, ip, ct);

        TempData["StatusMessage"] = deleted ? "Post deleted." : "That post no longer exists.";
        return RedirectToAction(nameof(Index));
    }

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
