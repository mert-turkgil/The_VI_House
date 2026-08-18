using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminMembershipPlansController(IMembershipService membershipService, UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var plans = await membershipService.GetAllPlansAsync(ct);
        return View(plans);
    }

    [HttpGet]
    public IActionResult Create() => View(new AdminMembershipPlanFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminMembershipPlanFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var entity = form.ToEntity();
        var (adminId, ip) = CurrentActor();
        await membershipService.CreatePlanAsync(entity, adminId, ip, ct);

        TempData["StatusMessage"] = $"\"{entity.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var plan = await membershipService.GetPlanAsync(id, ct);
        if (plan is null) return NotFound();

        return View(AdminMembershipPlanFormViewModel.FromEntity(plan));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AdminMembershipPlanFormViewModel form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid) return View(form);

        var (adminId, ip) = CurrentActor();
        await membershipService.UpdatePlanAsync(form.ToEntity(), adminId, ip, ct);

        TempData["StatusMessage"] = "Changes saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await membershipService.ArchivePlanAsync(id, adminId, ip, ct);
        TempData["StatusMessage"] = "Plan archived.";
        return RedirectToAction(nameof(Index));
    }

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
