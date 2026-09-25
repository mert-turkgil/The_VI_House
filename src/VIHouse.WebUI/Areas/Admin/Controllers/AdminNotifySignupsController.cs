using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.ViewModels;

using VIHouse.WebUI.Areas.Admin;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// The launch list — every address left on the coming-soon page while the curtain was up — and the
/// one thing to do with it: tell those people the doors are open.
///
/// The records themselves stay read-only. These are people who asked to be told about one thing, and
/// an admin screen that could edit the record of what they asked for would make that record worth
/// less than the promise made to them on the form. Sending is the promise being kept: it stamps
/// NotifiedAt, and every mail carries a link to leave the list.
///
/// Not hidden when <c>Features:ComingSoon</c> goes off. The moment the curtain comes down is exactly
/// when this list is most useful, so tying its visibility to the flag would hide it on launch day.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Marketing)]
[Route("admin/notify-signups")]
public class AdminNotifySignupsController(
    INotifySignupRepository signups,
    INotifySignupService launchList,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    private const int PageSize = 50;

    [HttpGet("")]
    public async Task<IActionResult> Index(int page, CancellationToken ct) =>
        View(await BuildAsync(page, new LaunchAnnouncementForm(), ct));

    [HttpPost("announce")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Announce(LaunchAnnouncementForm form, CancellationToken ct)
    {
        if (!form.Confirmed)
            return await RefuseAsync(form, "Tick the box to confirm you have checked the email before it goes to the list.", ct);

        var result = await launchList.AnnounceAsync(
            ToMessage(form), form.IncludeAlreadyNotified,
            Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (!result.Ok) return await RefuseAsync(form, result.Message, ct);

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Sends the draft to the signed-in admin only, and puts the draft back on screen so it
    /// can be adjusted and sent for real.</summary>
    [HttpPost("test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(LaunchAnnouncementForm form, CancellationToken ct)
    {
        var admin = await userManager.GetUserAsync(User);
        if (admin?.Email is null) return await RefuseAsync(form, "Your account has no email address to send a test to.", ct);

        var result = await launchList.SendTestAsync(ToMessage(form), admin.Email, CultureInfo.CurrentUICulture.Name, ct);
        form.Confirmed = false;
        var model = await BuildAsync(1, form, ct);
        ViewData["StatusMessage"] = result.Message;
        return View(nameof(Index), model);
    }

    private async Task<IActionResult> RefuseAsync(LaunchAnnouncementForm form, string message, CancellationToken ct)
    {
        form.Confirmed = false;
        ViewData["StatusMessage"] = message;
        return View(nameof(Index), await BuildAsync(1, form, ct));
    }

    private async Task<AdminNotifySignupsViewModel> BuildAsync(int page, LaunchAnnouncementForm draft, CancellationToken ct)
    {
        // A stale or hand-typed page number shows the first page rather than a 400 — the same
        // treatment AdminEmailsController gives its query string.
        var current = Math.Max(page, 1);

        return new AdminNotifySignupsViewModel
        {
            Page = current,
            PageSize = PageSize,
            Rows = await signups.GetRecentAsync((current - 1) * PageSize, PageSize, ct),
            TotalCount = await signups.CountAsync(ct),
            PendingCount = await launchList.CountRecipientsAsync(includeAlreadyNotified: false, ct),
            Draft = draft,
        };
    }

    private static LaunchAnnouncement ToMessage(LaunchAnnouncementForm form) =>
        new(form.Subject ?? "", form.Headline ?? "", form.Message ?? "", form.ButtonLabel, form.ButtonUrl);
}
