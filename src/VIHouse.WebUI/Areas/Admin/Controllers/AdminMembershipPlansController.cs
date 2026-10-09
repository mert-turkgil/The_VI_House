using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.ViewModels;

using VIHouse.WebUI.Areas.Admin;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Membership plans, and their mirror in Stripe. Every save here is pushed to Stripe by
/// MembershipService; this controller adds the explicit actions — sync one, sync all, import,
/// delete — and shows the state of the mirror so an admin can see at a glance whether the price
/// on the public page is the price Stripe will charge.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Money)]
[Route("admin/plans")]
public class AdminMembershipPlansController(
    IMembershipService membershipService,
    IOptions<StripeOptions> stripe,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    [HttpGet("")]
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

    [HttpGet("new")]
    public IActionResult Create() => View(new AdminMembershipPlanFormViewModel());

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminMembershipPlanFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var entity = form.ToEntity();
        var (adminId, ip) = CurrentActor();
        var created = await membershipService.CreatePlanAsync(entity, adminId, ip, ct);

        Status(created.ProviderSyncError is null
            ? loc["Admin.MembershipPlans.Msg.Created", entity.Name].Value
            : loc["Admin.MembershipPlans.Msg.CreatedNotSynced", entity.Name, created.ProviderSyncError].Value, isError: created.ProviderSyncError is not null);
        return RedirectToAction(nameof(Edit), new { id = created.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var plan = await membershipService.GetPlanAsync(id, ct);
        if (plan is null) return NotFound();

        var form = AdminMembershipPlanFormViewModel.FromEntity(plan);
        form.Usage = await membershipService.GetPlanUsageAsync(id, ct);
        form.StripeLive = stripe.Value.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal);
        return View(form);
    }

    [HttpPost("{id:guid}")]
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
        Status(saved?.ProviderSyncError is null
            ? loc["Admin.MembershipPlans.Msg.Saved"].Value
            : loc["Admin.MembershipPlans.Msg.SavedNotSynced", saved.ProviderSyncError].Value, isError: saved?.ProviderSyncError is not null);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:guid}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await membershipService.ArchivePlanAsync(id, adminId, ip, ct);
        Status(loc["Admin.MembershipPlans.Msg.Archived"].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await membershipService.DeletePlanAsync(id, adminId, ip, ct);

        TempData[result.Success ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return result.Success ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:guid}/sync")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await membershipService.SyncPlanAsync(id, adminId, ip, ct);
        TempData[result.Success ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("sync-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncAll(CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var summary = await membershipService.SyncAllPlansAsync(adminId, ip, ct);

        if (summary.Failed == 0)
            Status(loc["Admin.MembershipPlans.Msg.AllSynced", summary.Synced].Value);
        else
            Status(loc["Admin.MembershipPlans.Msg.SyncPartial", summary.Synced, summary.Failed, string.Join(" · ", summary.Errors)].Value, isError: true);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var summary = await membershipService.ImportPlansFromProviderAsync(adminId, ip, ct);

        if (summary.Error is not null)
            Status(loc["Admin.MembershipPlans.Msg.ImportFailed", summary.Error].Value, isError: true);
        else if (summary.Imported == 0)
            Status(loc["Admin.MembershipPlans.Msg.NothingNew", summary.Skipped].Value);
        else
            Status(loc["Admin.MembershipPlans.Msg.Imported", summary.Imported, summary.Skipped].Value);

        return RedirectToAction(nameof(Index));
    }
}
