using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Entities.Referrals;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Influencers' withdrawal requests — open ones first, oldest first — with the full bank details
/// Finance pays to. Paying records the payout for everything owed in that currency (the same
/// stale-balance guard as Influencers › Record payout); rejecting needs a reason the influencer sees.
/// Only Finance and SuperAdmin come here.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Money)]
[Route("admin/withdrawals")]
public class AdminWithdrawalsController(IAmbassadorService ambassadorService, IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await ambassadorService.GetWithdrawalQueueAsync(ct));

    [HttpPost("{id:guid}/pay")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(Guid id, long expectedOwedMinor, string? reference, string? note, bool confirmed, CancellationToken ct)
    {
        if (!confirmed)
        {
            Status(loc["Admin.Ambassadors.Msg.ConfirmTransfer"].Value, isError: true);
            return RedirectToAction(nameof(Index));
        }

        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.PayWithdrawalAsync(id, expectedOwedMinor, reference, note, adminId, ip, ct);
        Status(result.Success
            ? loc["Admin.Withdrawals.Msg.Paid", MoneyFormatter.Format(result.Payout!.AmountMinor, result.Payout.Currency)].Value
            : loc[result.Error!, result.ErrorArgs].Value, isError: !result.Success);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Status(loc["Admin.Ambassadors.Msg.ReasonNeeded"].Value, isError: true);
            return RedirectToAction(nameof(Index));
        }

        var (adminId, ip) = CurrentActor();
        var done = await ambassadorService.RejectWithdrawalAsync(id, reason.Trim(), adminId, ip, ct);
        Status(loc[done ? "Admin.Withdrawals.Msg.Rejected" : "Influencer.Error.RequestNotOpen"].Value, isError: !done);
        return RedirectToAction(nameof(Index));
    }
}
