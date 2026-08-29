using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Commerce;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Discount codes (brief §50). The redemption engine has been in the checkout path since Phase 1 —
/// `IPromoCodeRepository.TryRedeemAsync` and its no-oversell guard — but there was no screen, so a
/// code could only be created with SQL. This is that screen.
///
/// Every change is audit-logged: a promo code is money, and "who issued the one that took €250 off
/// twelve bookings" is a question that gets asked after the fact, not before.
/// </summary>
public class AdminPromoCodesController(
    IPromoCodeRepository promoCodes,
    IExperienceService experienceService,
    IAuditLogRepository auditLogs,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var all = await promoCodes.GetAllAsync(ct);
        var experiences = await experienceService.GetAllForAdminAsync(ct);

        var model = all
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new AdminPromoCodeListItemViewModel
            {
                Id = c.Id,
                Code = c.Code,
                Type = c.Type,
                Value = c.Value,
                Currency = c.Currency,
                ExperienceTitle = experiences.FirstOrDefault(e => e.Id == c.ExperienceId)?.Title,
                RedemptionCount = c.RedemptionCount,
                MaxRedemptions = c.MaxRedemptions,
                ExpiresAt = c.ExpiresAt,
                IsActive = c.IsActive,
            })
            .ToList();

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await PopulateExperiencesAsync(ct);
        return View("Edit", new AdminPromoCodeFormViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var code = await promoCodes.GetByIdAsync(id, ct);
        if (code is null) return NotFound();

        await PopulateExperiencesAsync(ct);
        ViewData["RedemptionCount"] = code.RedemptionCount;
        return View(AdminPromoCodeFormViewModel.FromEntity(code));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AdminPromoCodeFormViewModel form, CancellationToken ct)
    {
        if (form.Validate() is { } error)
            ModelState.AddModelError(nameof(form.Value), error);

        // The code has to be unique: two rows with the same code means a redemption decrements
        // whichever the query happened to return, and the other one never runs out.
        var clash = await promoCodes.GetByCodeAsync(form.Code.Trim().ToUpperInvariant(), ct);
        if (clash is not null && clash.Id != form.Id)
            ModelState.AddModelError(nameof(form.Code), "That code already exists.");

        if (!ModelState.IsValid)
        {
            await PopulateExperiencesAsync(ct);
            return View("Edit", form);
        }

        var (adminId, ip) = CurrentActor();

        if (form.Id is { } id && await promoCodes.GetByIdAsync(id, ct) is { } existing)
        {
            var before = new { existing.Code, existing.Type, existing.Value, existing.IsActive, existing.MaxRedemptions };

            var updated = form.ToEntity();
            existing.Code = updated.Code;
            existing.Type = updated.Type;
            existing.Value = updated.Value;
            existing.Currency = updated.Currency;
            existing.ExperienceId = updated.ExperienceId;
            existing.MaxRedemptions = updated.MaxRedemptions;
            existing.ExpiresAt = updated.ExpiresAt;
            existing.IsActive = updated.IsActive;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            // RedemptionCount is deliberately not editable. It is a record of what happened, and an
            // admin who could reset it could hand out an unlimited code that looks limited.

            await LogAsync("PromoCodeUpdated", existing.Id, adminId, ip, before,
                new { existing.Code, existing.Type, existing.Value, existing.IsActive, existing.MaxRedemptions }, ct);
            await promoCodes.SaveChangesAsync(ct);
            TempData["StatusMessage"] = $"\"{existing.Code}\" saved.";
        }
        else
        {
            var created = form.ToEntity();
            await promoCodes.AddAsync(created, ct);
            await LogAsync("PromoCodeCreated", created.Id, adminId, ip, before: null,
                after: new { created.Code, created.Type, created.Value, created.MaxRedemptions, created.ExpiresAt }, ct);
            await promoCodes.SaveChangesAsync(ct);
            TempData["StatusMessage"] = $"\"{created.Code}\" created.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Switches a code off rather than deleting it, when it has been used. A redeemed code is part
    /// of the record behind a discounted booking; removing the row would leave that booking's price
    /// unexplainable.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var code = await promoCodes.GetByIdAsync(id, ct);
        if (code is null)
        {
            TempData["StatusMessage"] = "That code no longer exists.";
            return RedirectToAction(nameof(Index));
        }

        var (adminId, ip) = CurrentActor();

        if (code.RedemptionCount > 0)
        {
            code.IsActive = false;
            code.UpdatedAt = DateTimeOffset.UtcNow;
            await LogAsync("PromoCodeDeactivated", code.Id, adminId, ip,
                before: new { code.Code, code.IsActive }, after: new { code.Code, IsActive = false }, ct);
            await promoCodes.SaveChangesAsync(ct);

            TempData["StatusMessage"] = $"\"{code.Code}\" has been redeemed {code.RedemptionCount} time(s), so it was switched off rather than deleted.";
            return RedirectToAction(nameof(Index));
        }

        promoCodes.Remove(code);
        await LogAsync("PromoCodeDeleted", code.Id, adminId, ip,
            before: new { code.Code, code.Type, code.Value }, after: null, ct);
        await promoCodes.SaveChangesAsync(ct);

        TempData["StatusMessage"] = $"\"{code.Code}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateExperiencesAsync(CancellationToken ct)
    {
        var experiences = await experienceService.GetAllForAdminAsync(ct);
        ViewData["Experiences"] = experiences
            .OrderBy(e => e.Title)
            .Select(e => new SelectListItem($"{e.Title} — {e.City}", e.Id.ToString()))
            .ToList();
    }

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(PromoCode),
            EntityId = entityId,
            DataBefore = before is null ? null : System.Text.Json.JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : System.Text.Json.JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
