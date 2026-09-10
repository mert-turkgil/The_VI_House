using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Abstract;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// The launch list — every address left on the coming-soon page while the curtain was up.
///
/// Read-only, like the message log next to it. These are people who asked to be told about one
/// thing, and an admin screen that could edit the record of what they asked for would make that
/// record worth less than the promise made to them on the form.
///
/// Not hidden when <c>Features:ComingSoon</c> goes off. The moment the curtain comes down is exactly
/// when this list is most useful, so tying its visibility to the flag would hide it on launch day.
/// </summary>
public class AdminNotifySignupsController(INotifySignupRepository signups) : AdminControllerBase
{
    private const int PageSize = 50;

    public async Task<IActionResult> Index(int page, CancellationToken ct)
    {
        // A stale or hand-typed page number shows the first page rather than a 400 — the same
        // treatment AdminEmailsController gives its query string.
        var current = Math.Max(page, 1);

        return View(new AdminNotifySignupsViewModel
        {
            Page = current,
            PageSize = PageSize,
            Rows = await signups.GetRecentAsync((current - 1) * PageSize, PageSize, ct),
            TotalCount = await signups.CountAsync(ct),
        });
    }
}
