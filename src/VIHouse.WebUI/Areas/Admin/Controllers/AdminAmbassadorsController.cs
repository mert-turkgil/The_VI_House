using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.ViewModels;

using VIHouse.WebUI.Areas.Admin;

using Microsoft.Extensions.Options;

using VIHouse.Business.Options;

using VIHouse.Entities.Referrals;

using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

[Authorize(Roles = AdminSections.RolesFor.Ambassadors)]
[Route("admin/ambassadors")]
public class AdminAmbassadorsController(
    IAmbassadorService ambassadorService,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IOptions<SiteOptions> siteOptions) : AdminControllerBase
{
    /// <summary>The public referral link. Site:BaseUrl when configured (the host visitors use),
    /// otherwise whatever this request came in on — right locally, right enough elsewhere.</summary>
    private string ReferralUrlFor(string code) => SiteUrls.Absolute(BaseUrl, SiteUrls.Referral(code));

    private string BaseUrl =>
        string.IsNullOrWhiteSpace(siteOptions.Value.BaseUrl) ? $"{Request.Scheme}://{Request.Host}" : siteOptions.Value.BaseUrl;

    private async Task<AdminAmbassadorEditViewModel> BuildEditModelAsync(Ambassador ambassador, AdminAmbassadorEditViewModel? form, CancellationToken ct)
    {
        var user = ambassador.UserId is { } userId ? await userManager.FindByIdAsync(userId.ToString()) : null;
        var model = form ?? AdminAmbassadorEditViewModel.FromEntity(ambassador, user?.Email);
        model.Ambassador = ambassador;
        model.AccountName = user is null ? null : $"{user.FirstName} {user.LastName}".Trim();
        model.Id = ambassador.Id;
        model.Code = ambassador.Code;
        model.Email = user?.Email ?? ambassador.InviteEmail;
        model.UserId = ambassador.UserId;
        model.CreatedAt = ambassador.CreatedAt;
        model.ReferralUrl = ReferralUrlFor(ambassador.Code);
        model.QrSvg = QrSvg.Render(model.ReferralUrl);
        model.Stats = await ambassadorService.GetStatsAsync(ambassador.Id, ct);
        model.Conversions = await ambassadorService.GetConversionsAsync(ambassador.Id, 30, ct);
        model.VisitSources = await ambassadorService.GetVisitSourcesAsync(ambassador.Id, ct);
        model.Payouts = await ambassadorService.GetPayoutsAsync(ambassador.Id, ct);
        model.Signals = await ambassadorService.GetFraudSignalsAsync(ambassador.Id, ct);
        model.CanSettle = User.IsInRole(Roles.SuperAdmin) || User.IsInRole(Roles.Finance);
        model.Links = new VIHouse.WebUI.ViewModels.Ambassador.ReferralLinksViewModel
        {
            Code = ambassador.Code,
            BaseUrl = BaseUrl,
            Targets = await ambassadorService.GetLinkTargetsAsync(ct),
            Stats = model.Stats.Targets,
            Style = "admin",
        };
        return model;
    }

    [HttpGet("")]

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var ambassadors = await ambassadorService.GetAllAsync(ct);
        return View(ambassadors.OrderBy(a => a.Name).ToList());
    }

    [HttpGet("new")]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public IActionResult Create() => View(new AdminAmbassadorCreateViewModel());

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> Create(AdminAmbassadorCreateViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.InviteAsync(
            new AmbassadorInvite(form.Email.Trim(), form.Name.Trim(), form.Code.Trim().ToUpperInvariant(), form.CommissionPercent, form.Culture),
            adminId, ip, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            if (result.UserId is { } existingUserId) ViewData["ExistingUserId"] = existingUserId;
            return View(form);
        }

        // The link itself is never shown here — it only exists in the email.
        TempData["StatusMessage"] = result.Error
            ?? $"Invitation sent to {form.Email.Trim()}. The code {result.Ambassador!.Code} is reserved; the links go live once they accept.";
        return RedirectToAction(nameof(Edit), new { id = result.Ambassador!.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var ambassador = await ambassadorService.GetByIdAsync(id, ct);
        if (ambassador is null) return NotFound();

        return View(await BuildEditModelAsync(ambassador, null, ct));
    }

    /// <summary>
    /// Emails the ambassador their link. Admins were copying "/r/CODE" out of the panel and
    /// pasting it into a mail by hand; this sends the absolute URL, the code and a personal
    /// note, from the House, to the address on the account.
    /// </summary>
    [HttpPost("{id:guid}/send-link")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> SendLink(Guid id, string? note, CancellationToken ct)
    {
        var ambassador = await ambassadorService.GetByIdAsync(id, ct);
        if (ambassador is null) return NotFound();

        if (ambassador.Status == AmbassadorStatus.Pending)
        {
            TempData["StatusMessage"] = "The referral link does not work until they accept the invitation.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var user = ambassador.UserId is { } userId ? await userManager.FindByIdAsync(userId.ToString()) : null;
        if (user?.Email is null)
        {
            TempData["StatusMessage"] = "This ambassador has no email address on their account.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var referralUrl = ReferralUrlFor(ambassador.Code);
        var sent = await emailService.SendAsync(
            "AmbassadorLink", user.Email, "Your VI House referral link",
            new AmbassadorLinkEmailModel(ambassador.Name, referralUrl, ambassador.Code, ambassador.CommissionPercent,
                referralUrl.Replace($"/r/{ambassador.Code}", "/ambassador"), string.IsNullOrWhiteSpace(note) ? null : note.Trim()),
            user.PreferredCulture ?? SiteCultures.Default,
            nameof(Ambassador), ambassador.Id, ct);

        TempData["StatusMessage"] = sent
            ? $"Link sent to {user.Email}."
            : $"The email to {user.Email} could not be sent — check Emails & SMS for the error. The link is still {referralUrl}.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>New link and expiry for a pending invitation; the old link stops working. The
    /// address and language can be corrected here — the fix for a mistyped email.</summary>
    [HttpPost("{id:guid}/resend-invite")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> ResendInvite(Guid id, string? email, string? culture, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Trim()))
        {
            TempData["StatusMessage"] = $"\"{email}\" is not an email address.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.ResendInviteAsync(id, email, culture, adminId, ip, ct);
        TempData["StatusMessage"] = !result.Success
            ? result.Error + (result.UserId is { } existing ? $" ({Url.Action("Details", "AdminUsers", new { id = existing })})" : "")
            : result.Error ?? $"A new invitation is on its way to {result.Ambassador!.InviteEmail}. The previous link no longer works.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>Cancels a pending invitation and frees the code.</summary>
    [HttpPost("{id:guid}/withdraw-invite")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> WithdrawInvite(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        if (!await ambassadorService.WithdrawInviteAsync(id, adminId, ip, ct))
        {
            TempData["StatusMessage"] = "Only a pending invitation can be withdrawn.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        TempData["StatusMessage"] = "Invitation withdrawn; the code is free again.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Records that everything owed in one currency has been paid. The form carries the balance
    /// the admin saw; if it has moved since, nothing is recorded (see MarkCommissionPaidAsync).
    /// Recording only — the money itself is sent outside the site (bank transfer).
    /// </summary>
    [HttpPost("{id:guid}/payouts")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Money)]
    public async Task<IActionResult> RecordPayout(Guid id, string currency, long expectedOwedMinor, string? reference, string? note, bool confirmed, CancellationToken ct)
    {
        if (!confirmed)
        {
            TempData["StatusMessage"] = "Tick the box to confirm the transfer has actually been made.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.MarkCommissionPaidAsync(id, currency ?? "", expectedOwedMinor, reference, note, adminId, ip, ct);
        TempData["StatusMessage"] = result.Success
            ? $"Recorded: {MoneyFormatter.Format(result.Payout!.AmountMinor, result.Payout.Currency)} paid{(result.Payout.Reference is null ? "" : $" (ref. {result.Payout.Reference})")}. The ambassador has been notified."
            : result.Error;
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>Strikes one ledger line's commission out — a self-referral, or anything else the
    /// House will not pay for. The line stays on the timeline, marked, with the reason.</summary>
    [HttpPost("{id:guid}/conversions/{conversionId:guid}/void")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Money)]
    public async Task<IActionResult> VoidConversion(Guid id, Guid conversionId, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["StatusMessage"] = "Give a reason for withdrawing the commission — it is kept on the record.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();
        var done = await ambassadorService.VoidConversionAsync(id, conversionId, reason.Trim(), adminId, ip, ct);
        TempData["StatusMessage"] = done ? "Commission withdrawn for that line." : "That line has no commission to withdraw (already withdrawn, or not a purchase).";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> Edit(Guid id, AdminAmbassadorEditViewModel form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid)
        {
            var ambassador = await ambassadorService.GetByIdAsync(id, ct);
            if (ambassador is null) return NotFound();
            return View(await BuildEditModelAsync(ambassador, form, ct));
        }

        var (adminId, ip) = CurrentActor();
        await ambassadorService.UpdateAsync(form.ToEntity(), adminId, ip, ct);

        TempData["StatusMessage"] = "Changes saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
