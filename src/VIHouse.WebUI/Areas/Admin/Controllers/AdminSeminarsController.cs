using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Seminars;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Authoring for "The VI House Sessions" — create the session, write it in the rich text editor,
/// upload the footage, translate it into the other three languages, price it, then publish.
///
/// The screen is deliberately split into independent forms (core fields, one per language, media)
/// each posting to its own action. A single giant bound form would mean that saving the price
/// re-posts — and can therefore silently clobber — a body someone is halfway through writing in
/// another tab.
///
/// Every failure that can reach an admin comes back from SeminarService as a resource key and is
/// resolved through <see cref="Localised"/>, so the panel speaks whichever language the admin set.
/// </summary>
public class AdminSeminarsController(
    ISeminarService seminarService,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    // --- Index / create --------------------------------------------------------------------------

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var all = await seminarService.GetAllForAdminAsync(ct);

        var model = all.Select(s => new AdminSeminarListItemViewModel
        {
            Id = s.Id,
            Slug = s.Slug,
            Title = SeminarContent.Title(s, SiteCultures.Default),
            Status = s.Status,
            Visibility = s.Visibility,
            StartAtUtc = s.StartAtUtc,
            PriceMinor = s.PriceMinor,
            Currency = s.Currency,
            IncludedWithMembership = s.IncludedWithMembership,
            TranslatedCultures = [.. s.Translations.Select(t => t.Culture).Order()],
        }).ToList();

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new AdminSeminarCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminSeminarCreateViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var (adminId, ip) = CurrentActor();

        // Created as a Draft regardless of what was posted: publishing has its own precondition
        // (a default-culture body must exist) and goes through SetStatus, which enforces it.
        var result = await seminarService.CreateAsync(
            form.Seminar.ToEntity(), form.DefaultTranslation.ToEntity(), adminId, ip, ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, Localised(result.Error));
            return View(form);
        }

        TempData["StatusMessage"] = loc["Admin.Seminar.Created", form.DefaultTranslation.Title].Value;
        return RedirectToAction(nameof(Edit), new { id = result.SeminarId });
    }

    // --- Edit ------------------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, string? culture, CancellationToken ct)
    {
        var seminar = await seminarService.GetForAdminEditAsync(id, ct);
        if (seminar is null) return NotFound();

        return View(await BuildEditModelAsync(seminar, culture, ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AdminSeminarFormViewModel form, CancellationToken ct)
    {
        form.Id = id;

        if (!ModelState.IsValid)
            return await RedisplayEditAsync(id, form, SiteCultures.Default, ct);

        var (adminId, ip) = CurrentActor();
        var result = await seminarService.UpdateAsync(form.ToEntity(), adminId, ip, ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, Localised(result.Error));
            return await RedisplayEditAsync(id, form, SiteCultures.Default, ct);
        }

        TempData["StatusMessage"] = loc["Admin.Seminar.Saved"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Translations ------------------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTranslation(Guid id, AdminSeminarTranslationFormViewModel form, CancellationToken ct)
    {
        form.SeminarId = id;

        if (!ModelState.IsValid)
        {
            var seminar = await seminarService.GetForAdminEditAsync(id, ct);
            if (seminar is null) return NotFound();

            // Re-render with the posted (invalid) copy in place rather than the stored version, so
            // the admin does not lose what they typed.
            var model = await BuildEditModelAsync(seminar, form.Culture, ct);
            model.Translations = [.. model.Translations.Select(tab =>
                tab.Culture.Name == form.Culture ? tab with { Form = form } : tab)];

            return View(nameof(Edit), model);
        }

        var (adminId, ip) = CurrentActor();
        var result = await seminarService.SaveTranslationAsync(id, form.ToEntity(), adminId, ip, ct);

        TempData["StatusMessage"] = result.Success
            ? loc["Admin.Seminar.TranslationSaved", SiteCultures.Describe(form.Culture).NativeLabel].Value
            : Localised(result.Error);

        return RedirectToAction(nameof(Edit), new { id, culture = form.Culture });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTranslation(Guid id, string culture, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await seminarService.DeleteTranslationAsync(id, culture, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success
            ? loc["Admin.Seminar.TranslationDeleted", SiteCultures.Describe(culture).NativeLabel].Value
            : Localised(result.Error);

        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Publishing --------------------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(Guid id, SeminarStatus status, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await seminarService.SetStatusAsync(id, status, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success
            ? loc["Admin.Seminar.Status." + status].Value
            : Localised(result.Error);

        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Media ---------------------------------------------------------------------------------------

    /// <summary>
    /// The media library's own upload form. Both limits are stated explicitly because the framework
    /// defaults (30 MB on the request, ~128 MB on the multipart body) are far below what a recording
    /// runs to, and the failure mode without them is a bare 413 with nothing on the page to explain
    /// it. Behind IIS, request filtering has its own maxAllowedContentLength that must be raised to
    /// match — these attributes do not reach it.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> UploadMedia(Guid id, IFormFile? file, string? title, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["StatusMessage"] = loc["Seminar.Error.MediaEmpty"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();

        await using var stream = file.OpenReadStream();
        var result = await seminarService.AddMediaAsync(
            id, new MediaUpload(file.FileName, file.ContentType, file.Length, stream),
            title, isInline: false, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success
            ? loc["Admin.Seminar.MediaAdded", file.FileName].Value
            : Localised(result.Error);

        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// The rich text editor's upload target (CKEditor's SimpleUploadAdapter). Returns the JSON shape
    /// that adapter expects: <c>{ url }</c> on success, <c>{ error: { message } }</c> otherwise —
    /// including on failure, since the adapter reads the body rather than the status code.
    ///
    /// The asset is recorded as inline, so it is access-controlled and cleaned up like everything
    /// else but is not repeated in the gallery under the article.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> UploadInline(Guid id, IFormFile? upload, CancellationToken ct)
    {
        if (upload is null || upload.Length == 0)
            return Json(new { error = new { message = loc["Seminar.Error.MediaEmpty"].Value } });

        var (adminId, ip) = CurrentActor();

        await using var stream = upload.OpenReadStream();
        var result = await seminarService.AddMediaAsync(
            id, new MediaUpload(upload.FileName, upload.ContentType, upload.Length, stream),
            title: null, isInline: true, adminId, ip, ct);

        if (!result.Success || result.Media is null)
            return Json(new { error = new { message = Localised(result.Error) } });

        // Slug-free by design — see ISeminarService.OpenMediaAsync. This URL is about to be written
        // into the article body, where it has to survive the session being renamed.
        return Json(new { url = Url.Action("Media", "Seminars", new { area = "", id = result.Media.Id }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMedia(Guid id, Guid mediaId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await seminarService.RemoveMediaAsync(id, mediaId, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success ? loc["Admin.Seminar.MediaRemoved"].Value : Localised(result.Error);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCover(Guid id, Guid mediaId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await seminarService.SetCoverAsync(id, mediaId, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success ? loc["Admin.Seminar.CoverSet"].Value : Localised(result.Error);
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Enrolments / delete ---------------------------------------------------------------------------

    public async Task<IActionResult> Enrolments(Guid id, CancellationToken ct)
    {
        var seminar = await seminarService.GetForAdminEditAsync(id, ct);
        if (seminar is null) return NotFound();

        var rows = new List<AdminSeminarEnrolmentRow>();
        foreach (var enrolment in await seminarService.GetEnrollmentsForAdminAsync(id, ct))
        {
            var user = await userManager.FindByIdAsync(enrolment.UserId.ToString());
            rows.Add(new AdminSeminarEnrolmentRow(
                user?.Email ?? "—",
                user is null ? "—" : $"{user.FirstName} {user.LastName}".Trim(),
                enrolment.Status, enrolment.GrantedVia,
                enrolment.AmountMinor, enrolment.Currency, enrolment.ConfirmedAt));
        }

        return View(new AdminSeminarEnrolmentsViewModel
        {
            SeminarId = seminar.Id,
            SeminarTitle = SeminarContent.Title(seminar, SiteCultures.Default),
            Slug = seminar.Slug,
            Rows = rows,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await seminarService.DeleteAsync(id, adminId, ip, ct);

        if (!result.Success)
        {
            TempData["StatusMessage"] = Localised(result.Error);
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["StatusMessage"] = loc["Admin.Seminar.Deleted"].Value;
        return RedirectToAction(nameof(Index));
    }

    // --- Helpers -----------------------------------------------------------------------------------------

    private async Task<AdminSeminarEditViewModel> BuildEditModelAsync(Seminar seminar, string? culture, CancellationToken ct)
    {
        var active = SiteCultures.Normalise(culture);

        // A tab per supported language whether or not it has been written — the whole point of this
        // screen is to make the missing translations obvious rather than hide them.
        var tabs = SiteCultures.All.Select(site =>
        {
            var existing = SeminarContent.Find(seminar, site.Name);
            return new AdminSeminarTranslationTab(
                site,
                IsWritten: existing is not null,
                IsDefault: site.Name == SiteCultures.Default,
                Form: existing is null
                    ? AdminSeminarTranslationFormViewModel.Empty(seminar.Id, site.Name)
                    : AdminSeminarTranslationFormViewModel.FromEntity(seminar.Id, existing));
        }).ToList();

        return new AdminSeminarEditViewModel
        {
            Form = AdminSeminarFormViewModel.FromEntity(seminar),
            Status = seminar.Status,
            PublishedAt = seminar.PublishedAt,
            CoverMediaId = seminar.CoverMediaId,
            Translations = tabs,
            Media = [.. seminar.Media.OrderBy(m => m.SortOrder)],
            EnrolmentCount = (await seminarService.GetEnrollmentsForAdminAsync(seminar.Id, ct))
                .Count(e => e.Status == SeminarEnrollmentStatus.Confirmed),
            ActiveCulture = active,
        };
    }

    /// <summary>Re-renders Edit with the admin's rejected core-fields input still in the boxes.</summary>
    private async Task<IActionResult> RedisplayEditAsync(Guid id, AdminSeminarFormViewModel form, string culture, CancellationToken ct)
    {
        var seminar = await seminarService.GetForAdminEditAsync(id, ct);
        if (seminar is null) return NotFound();

        var model = await BuildEditModelAsync(seminar, culture, ct);
        model.Form = form;
        return View(nameof(Edit), model);
    }

    /// <summary>
    /// Turns a service-layer error key into a sentence in the admin's own language. Falls back to
    /// the key when there is no entry, which is what IStringLocalizer does anyway — a visible
    /// "Seminar.Error.Whatever" on screen is a missing translation, and easier to chase than a
    /// silently blank message.
    /// </summary>
    private string Localised(string? errorKey) =>
        string.IsNullOrWhiteSpace(errorKey) ? loc["Seminar.Error.Unknown"].Value : loc[errorKey].Value;

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
