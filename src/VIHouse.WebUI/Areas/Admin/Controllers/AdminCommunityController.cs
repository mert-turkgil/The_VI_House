using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Community;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Seminars;
using Microsoft.AspNetCore.Mvc.Rendering;
using VIHouse.WebUI.Areas.Admin.ViewModels;

using VIHouse.WebUI.Areas.Admin;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Manages the members-only community destinations — the Discord invite, broadcast links, recurring
/// calls. These change often enough (a revoked invite, a new season's stream) that editing them must
/// not require a deploy.
///
/// Every change is audit-logged with the URL, because handing out or withdrawing an invite link is a
/// real access-control action even though it looks like content editing.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Marketing)]
public class AdminCommunityController(
    IRepository<CommunityLink> links,
    IAuditLogRepository auditLogs,
    IMembershipService membershipService,
    IRepository<Experience> experiences,
    IRepository<Seminar> seminars,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    /// <summary>The three "only for…" selects on the form. Loaded on every render of it so a
    /// validation round-trip does not come back with empty dropdowns.</summary>
    private async Task LoadScopeOptionsAsync(CancellationToken ct)
    {
        ViewBag.Plans = (await membershipService.GetAllPlansAsync(ct))
            .OrderBy(p => p.SortOrder).Select(p => new SelectListItem(p.Name, p.Id.ToString())).ToList();
        ViewBag.Experiences = (await experiences.GetAllAsync(ct))
            .OrderByDescending(e => e.StartAtUtc).Select(e => new SelectListItem($"{e.City} — {e.StartAtUtc:MMM yyyy}", e.Id.ToString())).ToList();
        ViewBag.Seminars = (await seminars.GetAllAsync(ct))
            .OrderByDescending(s => s.StartAtUtc ?? DateTimeOffset.MinValue).Select(s => new SelectListItem(s.Slug, s.Id.ToString())).ToList();
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var all = await links.GetAllAsync(ct);
        return View(all.OrderBy(l => l.SortOrder).ThenBy(l => l.Label).ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadScopeOptionsAsync(ct);
        return View("Edit", new AdminCommunityLinkFormViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var link = await links.GetByIdAsync(id, ct);
        if (link is null) return NotFound();
        await LoadScopeOptionsAsync(ct);
        return View(AdminCommunityLinkFormViewModel.FromEntity(link));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminCommunityLinkFormViewModel form, CancellationToken ct)
    {
        if (form.ScopeError() is { } scopeError)
            ModelState.AddModelError(nameof(form.MembershipPlanId), scopeError);
        if (!ModelState.IsValid)
        {
            await LoadScopeOptionsAsync(ct);
            return View("Edit", form);
        }

        var (adminId, ip) = CurrentActor();

        if (form.Id is { } id && await links.GetByIdAsync(id, ct) is { } existing)
        {
            var before = new { existing.Label, existing.Url, existing.Kind, existing.IsActive };

            existing.Label = form.Label.Trim();
            existing.Description = form.Description?.Trim();
            existing.Url = form.Url.Trim();
            existing.Kind = form.Kind;
            existing.IsActive = form.IsActive;
            existing.SortOrder = form.SortOrder;
            existing.MembershipPlanId = form.MembershipPlanId;
            existing.ExperienceId = form.ExperienceId;
            existing.SeminarId = form.SeminarId;
            existing.DiscordChannelId = string.IsNullOrWhiteSpace(form.DiscordChannelId) ? null : form.DiscordChannelId.Trim();
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            await LogAsync("CommunityLinkUpdated", existing.Id, adminId, ip,
                before, new { existing.Label, existing.Url, existing.Kind, existing.IsActive }, ct);

            await links.SaveChangesAsync(ct);
            TempData["StatusMessage"] = "Changes saved.";
        }
        else
        {
            var created = form.ToEntity();
            await links.AddAsync(created, ct);
            await LogAsync("CommunityLinkCreated", created.Id, adminId, ip,
                before: null, after: new { created.Label, created.Url, created.Kind, created.IsActive }, ct);

            await links.SaveChangesAsync(ct);
            TempData["StatusMessage"] = $"\"{created.Label}\" added.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var link = await links.GetByIdAsync(id, ct);
        if (link is null)
        {
            TempData["StatusMessage"] = "That link no longer exists.";
            return RedirectToAction(nameof(Index));
        }

        var (adminId, ip) = CurrentActor();
        links.Remove(link);
        await LogAsync("CommunityLinkDeleted", link.Id, adminId, ip,
            before: new { link.Label, link.Url, link.Kind }, after: null, ct);
        await links.SaveChangesAsync(ct);

        TempData["StatusMessage"] = "Link deleted.";
        return RedirectToAction(nameof(Index));
    }

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(CommunityLink),
            EntityId = entityId,
            DataBefore = before is null ? null : System.Text.Json.JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : System.Text.Json.JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
