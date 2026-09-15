using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.ViewModels;

using VIHouse.WebUI.Areas.Admin;

using Microsoft.Extensions.Options;

using VIHouse.Business.Options;

using VIHouse.Entities.Referrals;

using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

[Authorize(Roles = AdminSections.RolesFor.Marketing)]
public class AdminAmbassadorsController(
    IAmbassadorService ambassadorService,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IOptions<SiteOptions> siteOptions) : AdminControllerBase
{
    /// <summary>The public referral link. Site:BaseUrl when configured (the host visitors use),
    /// otherwise whatever this request came in on — right locally, right enough elsewhere.</summary>
    private string ReferralUrlFor(string code)
    {
        var baseUrl = siteOptions.Value.BaseUrl?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = $"{Request.Scheme}://{Request.Host}";
        return $"{baseUrl}/r/{code}";
    }

    private async Task<AdminAmbassadorEditViewModel> BuildEditModelAsync(Ambassador ambassador, AdminAmbassadorEditViewModel? form, CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(ambassador.UserId.ToString());
        var model = form ?? AdminAmbassadorEditViewModel.FromEntity(ambassador, user?.Email);
        model.Id = ambassador.Id;
        model.Code = ambassador.Code;
        model.Email = user?.Email;
        model.UserId = ambassador.UserId;
        model.CreatedAt = ambassador.CreatedAt;
        model.ReferralUrl = ReferralUrlFor(ambassador.Code);
        model.QrSvg = QrSvg.Render(model.ReferralUrl);
        model.Stats = await ambassadorService.GetStatsAsync(ambassador.Id, ct);
        model.Conversions = await ambassadorService.GetConversionsAsync(ambassador.Id, 30, ct);
        model.VisitSources = await ambassadorService.GetVisitSourcesAsync(ambassador.Id, ct);
        return model;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var ambassadors = await ambassadorService.GetAllAsync(ct);
        return View(ambassadors.OrderBy(a => a.Name).ToList());
    }

    [HttpGet]
    public IActionResult Create() => View(new AdminAmbassadorCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminAmbassadorCreateViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.CreateAsync(form.Email.Trim(), form.Name.Trim(), form.Code.Trim(), form.CommissionPercent, adminId, ip, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(form);
        }

        var user = await userManager.FindByIdAsync(result.UserId!.Value.ToString());
        string? passwordSetupUrl = null;
        if (user is not null)
        {
            var rawToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
            passwordSetupUrl = Url.Page("/Account/ResetPassword", pageHandler: null,
                values: new { area = "Identity", code = encodedCode }, protocol: Request.Scheme);
        }

        TempData["StatusMessage"] = passwordSetupUrl is null
            ? $"\"{form.Name}\" created."
            : $"\"{form.Name}\" created. Share this password-setup link with them: {passwordSetupUrl}";
        return RedirectToAction(nameof(Edit), new { id = result.Ambassador!.Id });
    }

    [HttpGet]
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
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendLink(Guid id, string? note, CancellationToken ct)
    {
        var ambassador = await ambassadorService.GetByIdAsync(id, ct);
        if (ambassador is null) return NotFound();

        var user = await userManager.FindByIdAsync(ambassador.UserId.ToString());
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
            nameof(Ambassador), ambassador.Id, ct);

        TempData["StatusMessage"] = sent
            ? $"Link sent to {user.Email}."
            : $"The email to {user.Email} could not be sent — check Emails & SMS for the error. The link is still {referralUrl}.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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
