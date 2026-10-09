using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
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
public class AdminFoundersController(IFounderService founders, IStringLocalizer<SharedResource> loc) : AdminControllerBase
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
            Status(loc["Admin.Users.Msg.NotSaved", string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))].Value, isError: true);
            return RedirectToAction(nameof(Index));
        }

        await founders.UpdateProgrammeAsync(new FounderProgramme(
            UtcDates.ToOffset(form.WindowEndsAtUtc), form.ExtraDiscountPercent, form.EarlyAccessDays, form.BadgeEnabled),
            CurrentAdminId(), Ip(), ct);

        Status(loc["Admin.Founders.Msg.Saved"].Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("backfill")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Backfill(CancellationToken ct)
    {
        var programme = await founders.GetProgrammeAsync(ct);
        if (programme.WindowEndsAtUtc is null)
        {
            Status(loc["Admin.Founders.Msg.NoWindow"].Value, isError: true);
            return RedirectToAction(nameof(Index));
        }

        var granted = await founders.BackfillAsync(CurrentAdminId(), Ip(), ct);
        Status(granted == 0
            ? loc["Admin.Founders.Msg.NoneToBackfill"].Value
            : loc["Admin.Founders.Msg.Backfilled", granted].Value);
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
    [Display(Name = "Admin.Field.FounderWindowEndsAtUtc")]
    public DateTime? WindowEndsAtUtc { get; set; }

    [Range(0, 100)]
    [Display(Name = "Admin.Field.FounderExtraDiscount")]
    public int ExtraDiscountPercent { get; set; }

    [Range(0, 365)]
    [Display(Name = "Admin.Field.FounderEarlyAccessDays")]
    public int EarlyAccessDays { get; set; }

    [Display(Name = "Admin.Field.FounderBadgeEnabled")]
    public bool BadgeEnabled { get; set; }
}
