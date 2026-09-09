using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Settings;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// How the site presents itself to everything that is not a person reading it: search engines, the
/// link preview in a chat app, a knowledge panel, a language model.
///
/// Split into independent forms rather than one giant bound page, for the reason the hero slide
/// editor gives: uploading a social image should not re-post, and therefore be able to clobber, a
/// description somebody is halfway through writing in another tab.
///
/// The language tabs follow the page-per-language convention (?culture=de-DE) already used by the
/// experience and seminar editors — the URL being shareable is the point, so a half-written German
/// draft can be bookmarked and handed to whoever actually writes the German.
/// </summary>
public class AdminSiteSettingsController(
    ISiteSettingsService settingsService,
    ISitemapService sitemap,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    /// <summary>Who is making the change, for the audit log. Same helper every admin controller has.</summary>
    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());

    public async Task<IActionResult> Index(string? culture, CancellationToken ct)
    {
        var settings = await settingsService.GetAsync(ct);
        var active = SiteCultures.IsSupported(culture) ? SiteCultures.Normalise(culture) : SiteCultures.Default;

        // Counting what the sitemap will actually contain, rather than describing it, so the screen
        // reports the real number and a mistake in the listing query is visible here rather than
        // three weeks later in Search Console.
        var entries = await sitemap.GetEntriesAsync(ct);

        var model = new AdminSiteSettingsViewModel
        {
            Form = AdminSiteSettingsForm.FromEntity(settings),
            ActiveCulture = active,
            Translations = [.. SiteCultures.All.Select(c =>
            {
                var row = settings.Translations.FirstOrDefault(t => t.Culture == c.Name);
                return new AdminSiteSettingsTranslationTab(
                    c,
                    IsWritten: row is not null,
                    Form: row is null
                        ? AdminSiteSettingsTranslationForm.Empty(settings.Id, c.Name)
                        : AdminSiteSettingsTranslationForm.FromEntity(row));
            })],
            LogoPreviewUrl = settings.LogoStorageKey is not null
                ? $"/media/site-logo/{settings.Id}?v={Stamp(settings)}"
                : settings.LogoUrl,
            OgImagePreviewUrl = settings.DefaultOgImageStorageKey is not null
                ? $"/media/site-og-default/{settings.Id}?v={Stamp(settings)}"
                : settings.DefaultOgImageUrl,
            HasUploadedLogo = settings.LogoStorageKey is not null,
            HasUploadedOgImage = settings.DefaultOgImageStorageKey is not null,
            SitemapUrlCount = entries.Sum(e => e.Cultures.Count),
            SitemapPageCount = entries.Count,
        };

        foreach (var tab in model.Translations)
        {
            var row = settings.Translations.FirstOrDefault(t => t.Culture == tab.Culture.Name);
            if (row?.OgImageStorageKey is not null)
                model.CultureOgImageUrls[tab.Culture.Name] = $"/media/site-og/{row.Id}?v={Stamp(settings)}";
            else if (!string.IsNullOrWhiteSpace(row?.OgImageUrl))
                model.CultureOgImageUrls[tab.Culture.Name] = row.OgImageUrl;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AdminSiteSettingsForm form, string? culture, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["StatusMessage"] = "Check the highlighted fields.";
            return RedirectToAction(nameof(Index), new { culture });
        }

        var (adminId, ip) = CurrentActor();
        await settingsService.UpdateAsync(form.ToEntity(), adminId, ip, ct);

        TempData["StatusMessage"] = "Site settings saved.";
        return RedirectToAction(nameof(Index), new { culture });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTranslation(AdminSiteSettingsTranslationForm form, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var error = await settingsService.SaveTranslationAsync(form.ToEntity(), adminId, ip, ct);

        TempData["StatusMessage"] = error switch
        {
            null => $"{SiteCultures.Describe(form.Culture).NativeLabel} saved.",
            "Admin.Settings.TemplateNeedsPlaceholder" =>
                "The title template must contain {0} — that is where each page's own name goes.",
            "Admin.Settings.UnknownCulture" => "That is not a language this site speaks.",
            _ => "That could not be saved.",
        };

        return RedirectToAction(nameof(Index), new { culture = form.Culture });
    }

    // --- Images ---------------------------------------------------------------------------------
    // The size limits and form limits are the established pair on every upload action in the admin;
    // without both, a large file fails at a different layer with a far less useful message.

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 8 * 1024 * 1024)]
    public async Task<IActionResult> UploadOgImage(IFormFile? file, string? culture, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["StatusMessage"] = "Choose an image first.";
            return RedirectToAction(nameof(Index), new { culture });
        }

        await using var stream = file.OpenReadStream();
        var (adminId, ip) = CurrentActor();

        var error = await settingsService.UploadOgImageAsync(
            new MediaUpload(file.FileName, file.ContentType, file.Length, stream),
            string.IsNullOrWhiteSpace(culture) ? null : culture, adminId, ip, ct);

        TempData["StatusMessage"] = error is null
            ? "Social image uploaded."
            : "That file could not be used — images only.";

        return RedirectToAction(nameof(Index), new { culture });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveOgImage(string? culture, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await settingsService.RemoveOgImageAsync(string.IsNullOrWhiteSpace(culture) ? null : culture, adminId, ip, ct);

        TempData["StatusMessage"] = "Social image removed.";
        return RedirectToAction(nameof(Index), new { culture });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 8 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["StatusMessage"] = "Choose an image first.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = file.OpenReadStream();
        var (adminId, ip) = CurrentActor();

        var error = await settingsService.UploadLogoAsync(
            new MediaUpload(file.FileName, file.ContentType, file.Length, stream), adminId, ip, ct);

        TempData["StatusMessage"] = error is null ? "Logo uploaded." : "That file could not be used — images only.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLogo(CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await settingsService.RemoveLogoAsync(adminId, ip, ct);

        TempData["StatusMessage"] = "Logo removed.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Cache-busts a replaced image, the same way the experience cover URL does.</summary>
    private static long Stamp(SiteSetting settings) =>
        (settings.UpdatedAt ?? settings.CreatedAt).ToUnixTimeSeconds();
}
