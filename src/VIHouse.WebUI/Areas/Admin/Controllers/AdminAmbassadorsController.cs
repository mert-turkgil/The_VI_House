using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminAmbassadorsController(IAmbassadorService ambassadorService, UserManager<ApplicationUser> userManager) : AdminControllerBase
{
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

        var user = await userManager.FindByIdAsync(ambassador.UserId.ToString());
        var stats = await ambassadorService.GetStatsAsync(id, ct);

        var model = AdminAmbassadorEditViewModel.FromEntity(ambassador, user?.Email);
        model.Stats = stats;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AdminAmbassadorEditViewModel form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid)
        {
            form.Stats = await ambassadorService.GetStatsAsync(id, ct);
            return View(form);
        }

        var (adminId, ip) = CurrentActor();
        await ambassadorService.UpdateAsync(form.ToEntity(), adminId, ip, ct);

        TempData["StatusMessage"] = "Changes saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
