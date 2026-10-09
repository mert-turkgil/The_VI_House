using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// The founder programme: the window during which a new member becomes a Founder, what being one
/// earns (extra discount, early access, a badge — each off until switched on here), and the list
/// of who they are. Marketing runs it; the role itself can also be ticked by hand on a user.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Marketing)]
[Route("admin/founders")]
public class AdminFoundersController(IFounderService founders) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var programme = await founders.GetProgrammeAsync(ct);
        return View(new AdminFoundersViewModel
        {
            Form = new AdminFounderProgrammeForm
            {
                WindowEndsAtUtc = programme.WindowEndsAtUtc?.UtcDateTime,
                ExtraDiscountPercent = programme.ExtraDiscountPercent,
                EarlyAccessDays = programme.EarlyAccessDays,
                BadgeEnabled = programme.BadgeEnabled,
            },
            IsWindowOpen = programme.IsWindowOpen(DateTimeOffset.UtcNow),
            Founders = await founders.GetFoundersAsync(ct),
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([Bind(Prefix = "Form")] AdminFounderProgrammeForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["StatusMessage"] = "Not saved: " + string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Index));
        }

        await founders.UpdateProgrammeAsync(new FounderProgramme(
            UtcDates.ToOffset(form.WindowEndsAtUtc), form.ExtraDiscountPercent, form.EarlyAccessDays, form.BadgeEnabled),
            CurrentAdminId(), Ip(), ct);

        TempData["StatusMessage"] = "Founder programme saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("backfill")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Backfill(CancellationToken ct)
    {
        var programme = await founders.GetProgrammeAsync(ct);
        if (programme.WindowEndsAtUtc is null)
        {
            TempData["StatusMessage"] = "Set the founder window's end date first — backfill grants Founder to everyone whose first membership started before it.";
            return RedirectToAction(nameof(Index));
        }

        var granted = await founders.BackfillAsync(CurrentAdminId(), Ip(), ct);
        TempData["StatusMessage"] = granted == 0
            ? "Everyone who qualifies is already a Founder."
            : $"{granted} member{(granted == 1 ? "" : "s")} became Founder{(granted == 1 ? "" : "s")} and {(granted == 1 ? "has" : "have")} been sent the welcome email.";
        return RedirectToAction(nameof(Index));
    }
}

public class AdminFoundersViewModel
{
    public AdminFounderProgrammeForm Form { get; set; } = new();
    public bool IsWindowOpen { get; set; }
    public List<FounderListItem> Founders { get; set; } = [];
}

public class AdminFounderProgrammeForm
{
    [Display(Name = "Founder window closes (UTC)")]
    public DateTime? WindowEndsAtUtc { get; set; }

    [Range(0, 100)]
    [Display(Name = "Extra discount for Founders (%)")]
    public int ExtraDiscountPercent { get; set; }

    [Range(0, 365)]
    [Display(Name = "Early access (days before members)")]
    public int EarlyAccessDays { get; set; }

    [Display(Name = "Show the Founder badge to members")]
    public bool BadgeEnabled { get; set; }
}
