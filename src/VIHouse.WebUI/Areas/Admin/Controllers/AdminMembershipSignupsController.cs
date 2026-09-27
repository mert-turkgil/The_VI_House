using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Membership;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Every membership sign-up form — the /join answers — whether or not it was paid for.
///
/// Membership is not reviewed (anyone who pays is in, unlike an experience application), but the
/// House still wants to know who is joining and what they said, and a form that was filled in and
/// then abandoned at the card screen is worth seeing too. Read-only: these are the member's own
/// words, and the account (once paid) is where anything about them changes.
///
/// Open to every admin role, as requested — the answers include an address and an earnings band,
/// so narrow <see cref="AdminSections.RolesFor.Everyone"/> here if that ever needs to change.
/// Rows that never paid have their free text and address cleared by PendingJoinPurgeService after a
/// while; the page says so rather than showing blanks as if nothing was written.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Everyone)]
[Route("admin/membership-signups")]
public class AdminMembershipSignupsController(
    IPendingJoinRepository joins,
    IRepository<MembershipPlan> plans,
    IPromoCodeRepository promoCodes,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    private const int PageSize = 50;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, int page, CancellationToken ct)
    {
        PendingJoinStatus? parsed = Enum.TryParse<PendingJoinStatus>(status, out var s) ? s : null;
        var current = Math.Max(page, 1);
        var planNames = (await plans.GetAllAsync(ct)).ToDictionary(p => p.Id, p => p.Name);

        return View(new AdminMembershipSignupsViewModel
        {
            Status = parsed,
            Page = current,
            PageSize = PageSize,
            Rows = await joins.GetRecentAsync(parsed, (current - 1) * PageSize, PageSize, ct),
            TotalCount = await joins.CountAsync(parsed, ct),
            PaidCount = await joins.CountAsync(PendingJoinStatus.Paid, ct),
            PlanNames = planNames,
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var join = await joins.GetByIdAsync(id, ct);
        if (join is null) return NotFound();

        var plan = await plans.GetByIdAsync(join.PlanId, ct);
        var promo = join.PromoCodeId is { } promoId ? await promoCodes.GetByIdAsync(promoId, ct) : null;
        // The account the payment created — or one that already existed for this address.
        var account = join.UserId is { } userId
            ? await userManager.FindByIdAsync(userId.ToString())
            : await userManager.FindByEmailAsync(join.Email);

        return View(new AdminMembershipSignupDetailViewModel
        {
            Join = join,
            PlanName = plan?.Name ?? "—",
            PromoCode = promo?.Code,
            AccountId = account?.Id,
            AccountEverSignedIn = account?.LastLoginAt is not null,
        });
    }
}
