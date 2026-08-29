using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Seminars;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Authoring for The Journal — write the article, add its photography, audio and video, translate it
/// into the other three languages, publish.
///
/// The screen is split into independent forms (core fields, one per language, media) each posting to
/// its own action, exactly as the seminar editor is. A single bound form would mean that uploading a
/// file re-posts — and can therefore silently clobber — an article someone is halfway through
/// writing in another tab.
/// </summary>
public class AdminJournalController(
    IJournalService journalService,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    // --- Index / create ----------------------------------------------------------------------------

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var all = await journalService.GetAllForAdminAsync(ct);

        var model = all.Select(p => new AdminJournalListItemViewModel
        {
            Id = p.Id,
            Title = JournalContent.Title(p, SiteCultures.Default),
            Slug = p.Slug,
            Category = p.Category,
            Status = p.Status,
            PublishedAt = p.PublishedAt,
            TranslatedCultures = [.. p.Translations.Select(t => t.Culture).Order()],
        }).ToList();

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new AdminJournalCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminJournalCreateViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var (adminId, ip) = CurrentActor();
        var result = await journalService.CreateAsync(
            model.Post.ToEntity(), model.DefaultTranslation.ToEntity(), adminId, ip, ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, Localised(result.Error));
            return View(model);
        }

        TempData["StatusMessage"] = loc["Admin.Journal.Created", model.DefaultTranslation.Title].Value;
        return RedirectToAction(nameof(Edit), new { id = result.PostId });
    }

    // --- Edit --------------------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, string? culture, CancellationToken ct)
    {
        var post = await journalService.GetForAdminEditAsync(id, ct);
        if (post is null) return NotFound();

        return View(BuildEditModel(post, culture));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AdminJournalPostFormViewModel form, CancellationToken ct)
    {
        form.Id = id;

        var post = await journalService.GetForAdminEditAsync(id, ct);
        if (post is null) return NotFound();

        if (!ModelState.IsValid)
        {
            var invalid = BuildEditModel(post, SiteCultures.Default);
            invalid.Form = form;
            return View(invalid);
        }

        var (adminId, ip) = CurrentActor();
        var result = await journalService.UpdateAsync(form.ToEntity(), adminId, ip, ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, Localised(result.Error));
            var invalid = BuildEditModel(post, SiteCultures.Default);
            invalid.Form = form;
            return View(invalid);
        }

        TempData["StatusMessage"] = loc["Admin.Journal.Saved"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Translations ------------------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTranslation(Guid id, AdminJournalTranslationFormViewModel form, CancellationToken ct)
    {
        form.JournalPostId = id;

        var post = await journalService.GetForAdminEditAsync(id, ct);
        if (post is null) return NotFound();

        if (!ModelState.IsValid)
        {
            var invalid = BuildEditModel(post, form.Culture);
            invalid.Translations = [.. invalid.Translations.Select(tab =>
                tab.Culture.Name == form.Culture ? tab with { Form = form } : tab)];
            return View(nameof(Edit), invalid);
        }

        var (adminId, ip) = CurrentActor();
        var result = await journalService.SaveTranslationAsync(id, form.ToEntity(), adminId, ip, ct);

        TempData["StatusMessage"] = result.Success
            ? loc["Admin.Journal.TranslationSaved", SiteCultures.Describe(form.Culture).NativeLabel].Value
            : Localised(result.Error);

        return RedirectToAction(nameof(Edit), new { id, culture = form.Culture });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTranslation(Guid id, string culture, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.DeleteTranslationAsync(id, culture, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success
            ? loc["Admin.Journal.TranslationDeleted", SiteCultures.Describe(culture).NativeLabel].Value
            : Localised(result.Error);

        return RedirectToAction(nameof(Edit), new { id, culture });
    }

    // --- Media -------------------------------------------------------------------------------------

    /// <summary>
    /// The library uploader: photographs, GIFs and audio an author wants to place by hand. Its own
    /// multipart form, so it never carries the article's copy with it.
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
        var result = await journalService.AddMediaAsync(
            id, new MediaUpload(file.FileName, file.ContentType, file.Length, stream),
            title, isInline: false, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success
            ? loc["Admin.Journal.MediaAdded", file.FileName].Value
            : Localised(result.Error);

        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// The rich text editor's upload target (CKEditor's SimpleUploadAdapter). Returns the JSON shape
    /// that adapter expects: <c>{ url }</c> on success, <c>{ error: { message } }</c> otherwise —
    /// including on failure, since the adapter reads the body rather than the status code.
    ///
    /// Recorded as inline, which is what makes it eligible for pruning once no body references it.
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
        var result = await journalService.AddMediaAsync(
            id, new MediaUpload(upload.FileName, upload.ContentType, upload.Length, stream),
            title: null, isInline: true, adminId, ip, ct);

        if (!result.Success || result.Media is null)
            return Json(new { error = new { message = Localised(result.Error) } });

        return Json(new { url = JournalService.MediaUrl(result.Media.Id) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMedia(Guid id, Guid mediaId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.RemoveMediaAsync(id, mediaId, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success ? loc["Admin.Journal.MediaRemoved"].Value : Localised(result.Error);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCover(Guid id, Guid mediaId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.SetCoverAsync(id, mediaId, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success ? loc["Admin.Journal.CoverChanged"].Value : Localised(result.Error);
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>Uploads a cover and drops the one it replaces — file included.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> UploadCover(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["StatusMessage"] = loc["Seminar.Error.MediaEmpty"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();

        await using var stream = file.OpenReadStream();
        var result = await journalService.ReplaceCoverAsync(
            id, new MediaUpload(file.FileName, file.ContentType, file.Length, stream), adminId, ip, ct);

        TempData["StatusMessage"] = result.Success ? loc["Admin.Journal.CoverChanged"].Value : Localised(result.Error);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCover(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.RemoveCoverAsync(id, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success ? loc["Admin.Journal.CoverRemoved"].Value : Localised(result.Error);
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Delete ------------------------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.DeleteAsync(id, adminId, ip, ct);

        TempData["StatusMessage"] = result.Success
            ? loc["Admin.Journal.Deleted"].Value
            : loc["Admin.Journal.AlreadyGone"].Value;

        return RedirectToAction(nameof(Index));
    }

    // --- Helpers -----------------------------------------------------------------------------------

    private AdminJournalEditViewModel BuildEditModel(JournalPost post, string? culture)
    {
        var active = SiteCultures.IsSupported(culture) ? SiteCultures.Normalise(culture) : SiteCultures.Default;
        var cover = post.Media.FirstOrDefault(m => m.Id == post.CoverMediaId);

        return new AdminJournalEditViewModel
        {
            Form = AdminJournalPostFormViewModel.FromEntity(post),
            CoverMediaId = post.CoverMediaId,
            CoverPreviewUrl = cover is not null ? JournalService.MediaUrl(cover.Id) : post.CoverImageUrl,
            PublishedAt = post.PublishedAt,
            // Attachments only. An inline image is already in the article, and offering it here as
            // something to "remove" is offering to break the paragraph it sits in.
            Media = [.. post.Media.Where(m => !m.IsInline).OrderBy(m => m.SortOrder)],
            ActiveCulture = active,
            Translations = [.. SiteCultures.All.Select(c =>
            {
                var existing = JournalContent.Find(post, c.Name);
                return new AdminJournalTranslationTab(
                    c,
                    existing is not null,
                    c.Name == SiteCultures.Default,
                    existing is null
                        ? AdminJournalTranslationFormViewModel.Empty(post.Id, c.Name)
                        : AdminJournalTranslationFormViewModel.FromEntity(post.Id, existing));
            })],
        };
    }

    /// <summary>Service errors arrive as SharedResource keys, not sentences, so the panel speaks
    /// whichever language the admin set — same contract as AdminSeminarsController.</summary>
    private string Localised(string? key) =>
        string.IsNullOrWhiteSpace(key) ? loc["Admin.Journal.SaveFailed"].Value : loc[key].Value;

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
