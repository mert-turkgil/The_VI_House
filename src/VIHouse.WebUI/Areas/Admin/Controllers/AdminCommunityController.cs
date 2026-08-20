using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Community;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Manages the members-only community destinations — the Discord invite, broadcast links, recurring
/// calls. These change often enough (a revoked invite, a new season's stream) that editing them must
/// not require a deploy.
///
/// Every change is audit-logged with the URL, because handing out or withdrawing an invite link is a
/// real access-control action even though it looks like content editing.
/// </summary>
public class AdminCommunityController(
    IRepository<CommunityLink> links,
    IAuditLogRepository auditLogs,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var all = await links.GetAllAsync(ct);
        return View(all.OrderBy(l => l.SortOrder).ThenBy(l => l.Label).ToList());
    }

    [HttpGet]
    public IActionResult Create() => View("Edit", new AdminCommunityLinkFormViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var link = await links.GetByIdAsync(id, ct);
        if (link is null) return NotFound();

        return View(AdminCommunityLinkFormViewModel.FromEntity(link));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminCommunityLinkFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", form);

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
