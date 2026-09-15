using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Membership plans, and their mirror in Stripe. Every save here is pushed to Stripe by
/// MembershipService; this controller adds the explicit actions — sync one, sync all, import,
/// delete — and shows the state of the mirror so an admin can see at a glance whether the price
/// on the public page is the price Stripe will charge.
/// </summary>
public class AdminMembershipPlansController(
    IMembershipService membershipService,
    UserManager<ApplicationUser> userManager,
    IOptions<StripeOptions> stripe) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var plans = await membershipService.GetAllPlansAsync(ct);
        var availability = new Dictionary<Guid, PlanAvailability>();
        foreach (var plan in plans)
            availability[plan.Id] = await membershipService.GetPlanAvailabilityAsync(plan.Id, ct);
        ViewBag.Availability = availability;
        ViewData["StripeConfigured"] = !string.IsNullOrWhiteSpace(stripe.Value.SecretKey);
        ViewData["StripeLive"] = stripe.Value.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal);
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
        var created = await membershipService.CreatePlanAsync(entity, adminId, ip, ct);

        TempData["StatusMessage"] = created.ProviderSyncError is null
            ? $"\"{entity.Name}\" created and synced to Stripe."
            : $"\"{entity.Name}\" created, but it could not be synced to Stripe: {created.ProviderSyncError}";
        return RedirectToAction(nameof(Edit), new { id = created.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var plan = await membershipService.GetPlanAsync(id, ct);
        if (plan is null) return NotFound();

        var form = AdminMembershipPlanFormViewModel.FromEntity(plan);
        form.Usage = await membershipService.GetPlanUsageAsync(id, ct);
        form.StripeLive = stripe.Value.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal);
        return View(form);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AdminMembershipPlanFormViewModel form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid)
        {
            form.Usage = await membershipService.GetPlanUsageAsync(id, ct);
            return View(form);
        }

        var (adminId, ip) = CurrentActor();
        await membershipService.UpdatePlanAsync(form.ToEntity(), adminId, ip, ct);

        var saved = await membershipService.GetPlanAsync(id, ct);
        TempData["StatusMessage"] = saved?.ProviderSyncError is null
            ? "Changes saved and synced to Stripe."
            : $"Changes saved, but Stripe could not be updated: {saved.ProviderSyncError}";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await membershipService.ArchivePlanAsync(id, adminId, ip, ct);
        TempData["StatusMessage"] = "Plan archived — and taken off sale at Stripe.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await membershipService.DeletePlanAsync(id, adminId, ip, ct);

        TempData[result.Success ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await membershipService.SyncPlanAsync(id, adminId, ip, ct);
        TempData[result.Success ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncAll(CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var summary = await membershipService.SyncAllPlansAsync(adminId, ip, ct);

        if (summary.Failed == 0)
            TempData["StatusMessage"] = $"All {summary.Synced} plan(s) are in sync with Stripe.";
        else
            TempData["ErrorMessage"] = $"{summary.Synced} synced, {summary.Failed} failed: {string.Join(" · ", summary.Errors)}";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var summary = await membershipService.ImportPlansFromProviderAsync(adminId, ip, ct);

        if (summary.Error is not null)
            TempData["ErrorMessage"] = $"Could not read Stripe's product list: {summary.Error}";
        else if (summary.Imported == 0)
            TempData["StatusMessage"] = $"Nothing new at Stripe — {summary.Skipped} product(s) already have a plan here.";
        else
            TempData["StatusMessage"] = $"Imported {summary.Imported} plan(s) from Stripe as archived; review and activate the ones you want on sale. {summary.Skipped} already existed.";

        return RedirectToAction(nameof(Index));
    }

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
