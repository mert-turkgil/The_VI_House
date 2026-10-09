using Microsoft.AspNetCore.Authorization;
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

using VIHouse.WebUI.Areas.Admin;

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
[Authorize(Roles = AdminSections.RolesFor.Content)]
[Route("admin/journal")]
public class AdminJournalController(
    IJournalService journalService,
    IAmbassadorService ambassadors,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    // --- Index / create ----------------------------------------------------------------------------

    /// <param name="show">"submissions" lists only what influencers sent in and the editors still owe
    /// an answer — Submitted first, then the ones sent back.</param>
    [HttpGet("")]
    public async Task<IActionResult> Index(string? show, CancellationToken ct)
    {
        var all = await journalService.GetAllForAdminAsync(ct);
        var submissionsOnly = show == "submissions";
        ViewData["ShowSubmissions"] = submissionsOnly;
        ViewData["SubmittedCount"] = all.Count(p => p.Status == JournalPostStatus.Submitted);
        if (submissionsOnly)
            all = [.. all.Where(p => p.Status is JournalPostStatus.Submitted or JournalPostStatus.ChangesRequested)
                .OrderBy(p => p.Status != JournalPostStatus.Submitted).ThenBy(p => p.SubmittedAt)];

        var model = all.Select(p => new AdminJournalListItemViewModel
        {
            Id = p.Id,
            Title = JournalContent.Title(p, SiteCultures.Default),
            Slug = p.Slug,
            Category = p.Category,
            Status = p.Status,
            PublishedAt = p.PublishedAt,
            SubmittedBy = p.AuthorUserId is null ? null : p.AuthorName ?? "—",
            TranslatedCultures = [.. p.Translations.Select(t => t.Culture).Order()],
        }).ToList();

        return View(model);
    }

    [HttpGet("new")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        // The author field starts with the signed-in person's name: most posts are written by the
        // person writing them, and it is one field fewer between "New article" and writing.
        var user = await userManager.GetUserAsync(User);
        var name = user is null ? null : $"{user.FirstName} {user.LastName}".Trim();
        return View(new AdminJournalStartViewModel { AuthorName = string.IsNullOrWhiteSpace(name) ? null : name });
    }

    /// <summary>
    /// "New article" asks for a headline and a category, nothing more: the slug comes from the title
    /// and the body is written on the next screen, where image upload already works (a file needs a
    /// post to belong to). It used to demand a slug in a strict format and the whole article up front.
    /// </summary>
    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminJournalStartViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var (adminId, ip) = CurrentActor();
        var post = new JournalPost
        {
            Category = model.Category,
            Status = JournalPostStatus.Draft,
            AuthorName = string.IsNullOrWhiteSpace(model.AuthorName) ? null : model.AuthorName.Trim(),
        };
        var copy = new JournalPostTranslation
        {
            Title = model.Title.Trim(),
            Excerpt = string.IsNullOrWhiteSpace(model.Excerpt) ? null : model.Excerpt.Trim(),
            Body = string.Empty,
        };
        var result = await journalService.CreateAsync(post, copy, adminId, ip, ct);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, Localised(result.Error));
            return View(model);
        }

        Status(loc["Admin.Journal.Started", model.Title].Value);
        return RedirectToAction(nameof(Edit), new { id = result.PostId });
    }

    // --- The writer --------------------------------------------------------------------------------

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, string? culture, CancellationToken ct)
    {
        var post = await journalService.GetForAdminEditAsync(id, ct);
        if (post is null) return NotFound();

        return View(await WithSubmissionAsync(BuildEditModel(post, culture), post, ct));
    }

    /// <summary>
    /// Sends an influencer's submission back with a note they see in their area and by email. The
    /// article stays theirs to change; publishing is still this screen's Publish button.
    /// </summary>
    [HttpPost("{id:guid}/request-changes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestChanges(Guid id, string? note, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.RequestChangesAsync(id, note ?? "", adminId, ip, ct);
        Status(result.Success ? loc["Admin.Journal.ChangesRequested"].Value : Localised(result.Error), isError: !result.Success);
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>The "submitted by" banner for an influencer's article.</summary>
    private async Task<AdminJournalEditViewModel> WithSubmissionAsync(AdminJournalEditViewModel model, JournalPost post, CancellationToken ct)
    {
        if (post.AuthorUserId is not { } authorId) return model;
        var influencer = await ambassadors.GetByUserIdAsync(authorId, ct);
        model.Submission = new JournalSubmissionInfo(influencer?.Name ?? post.AuthorName ?? "—", influencer?.Id, post.Status, post.SubmittedAt, post.ReviewNote);
        return model;
    }

    /// <summary>
    /// The writer's one Save: the post's settings and the open language's copy together, so a writer
    /// never has to know which of two buttons saves which half of the screen. <paramref name="intent"/>
    /// is the button pressed: save (keeps the status), publish or unpublish.
    ///
    /// The copy is saved first, so "write the English article, press Publish" works in one step:
    /// publishing requires an English body, and it now exists by the time the status changes.
    /// </summary>
    [HttpPost("{id:guid}/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        Guid id,
        [Bind(Prefix = "Post")] AdminJournalPostFormViewModel post,
        [Bind(Prefix = "Copy")] AdminJournalTranslationFormViewModel copy,
        string? intent,
        CancellationToken ct)
    {
        var existing = await journalService.GetForAdminEditAsync(id, ct);
        if (existing is null) return NotFound();

        post.Id = id;
        copy.JournalPostId = id;
        post.Status = intent switch
        {
            "publish" => JournalPostStatus.Published,
            "unpublish" => JournalPostStatus.Draft,
            _ => existing.Status,
        };

        if (!ModelState.IsValid) return View(nameof(Edit), await WithSubmissionAsync(Redisplay(existing, post, copy), existing, ct));

        var (adminId, ip) = CurrentActor();
        var copyResult = await journalService.SaveTranslationAsync(id, copy.ToEntity(), adminId, ip, ct);
        if (!copyResult.Success)
        {
            ModelState.AddModelError(string.Empty, Localised(copyResult.Error));
            return View(nameof(Edit), await WithSubmissionAsync(Redisplay(existing, post, copy), existing, ct));
        }

        var postResult = await journalService.UpdateAsync(post.ToEntity(), adminId, ip, ct);
        if (!postResult.Success)
        {
            // The words are safe (saved above); only the settings or the status change were refused.
            ModelState.AddModelError(string.Empty, Localised(postResult.Error));
            var reloaded = await journalService.GetForAdminEditAsync(id, ct) ?? existing;
            post.Status = existing.Status;
            return View(nameof(Edit), await WithSubmissionAsync(Redisplay(reloaded, post, copy), reloaded, ct));
        }

        Status((intent, post.Status) switch
        {
            ("publish", _) => loc["Admin.Journal.Saved.Published"].Value,
            ("unpublish", _) => loc["Admin.Journal.Saved.Unpublished"].Value,
            (_, JournalPostStatus.Published) => loc["Admin.Journal.Saved.Live"].Value,
            _ => loc["Admin.Journal.Saved.Draft"].Value,
        });
        return RedirectToAction(nameof(Edit), new { id, culture = copy.Culture });
    }

    /// <summary>The writer again, with what was posted in place, so a refused save loses nothing.</summary>
    private AdminJournalEditViewModel Redisplay(JournalPost post, AdminJournalPostFormViewModel form, AdminJournalTranslationFormViewModel copy)
    {
        var model = BuildEditModel(post, copy.Culture);
        model.Form = form;
        model.Translations = [.. model.Translations.Select(tab => tab.Culture.Name == SiteCultures.Normalise(copy.Culture) ? tab with { Form = copy } : tab)];
        return model;
    }

    [HttpPost("{id:guid}/delete-translation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTranslation(Guid id, string culture, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.DeleteTranslationAsync(id, culture, adminId, ip, ct);

        Status(result.Success
            ? loc["Admin.Journal.TranslationDeleted", SiteCultures.Describe(culture).NativeLabel].Value
            : Localised(result.Error), isError: !result.Success);

        return RedirectToAction(nameof(Edit), new { id, culture });
    }

    // --- Media -------------------------------------------------------------------------------------

    /// <summary>
    /// The library uploader: photographs, GIFs and audio an author wants to place by hand. Its own
    /// multipart form, so it never carries the article's copy with it.
    /// </summary>
    [HttpPost("{id:guid}/upload-media")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> UploadMedia(Guid id, IFormFile? file, string? title, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            Status(loc["Seminar.Error.MediaEmpty"].Value, isError: true);
            return RedirectToAction(nameof(Edit), null, new { id }, "media-library");
        }

        var (adminId, ip) = CurrentActor();

        await using var stream = file.OpenReadStream();
        var result = await journalService.AddMediaAsync(
            id, new MediaUpload(file.FileName, file.ContentType, file.Length, stream),
            title, isInline: false, adminId, ip, ct);

        Status(!result.Success
            ? Localised(result.Error)
            : result.Reused
                ? loc["Admin.Journal.MediaReused", file.FileName].Value
                : loc["Admin.Journal.MediaAdded", file.FileName].Value, isError: !result.Success);

        return RedirectToAction(nameof(Edit), null, new { id }, "media-library");
    }

    /// <summary>
    /// The rich text editor's upload target (CKEditor's SimpleUploadAdapter). Returns the JSON shape
    /// that adapter expects: <c>{ url }</c> on success, <c>{ error: { message } }</c> otherwise —
    /// including on failure, since the adapter reads the body rather than the status code.
    ///
    /// Recorded as inline, which is what makes it eligible for pruning once no body references it.
    /// </summary>
    [HttpPost("{id:guid}/upload-inline")]
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

    [HttpPost("{id:guid}/remove-media")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMedia(Guid id, Guid mediaId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.RemoveMediaAsync(id, mediaId, adminId, ip, ct);

        Status(result.Success ? loc["Admin.Journal.MediaRemoved"].Value : Localised(result.Error), isError: !result.Success);
        return RedirectToAction(nameof(Edit), null, new { id }, "media-library");
    }

    /// <summary>
    /// Saves the media library as arranged on screen: order, captions per language, and which
    /// photographs the article's gallery shows. One save for all three, so dragging a photograph and
    /// fixing its caption cannot end up half-applied.
    /// </summary>
    [HttpPost("{id:guid}/save-gallery")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveGallery(Guid id, List<AdminJournalGalleryItemForm> items, string? culture, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.SaveGalleryAsync(
            id, [.. items.Select(i => new JournalGalleryEdit(i.MediaId, i.Captions, i.ShowInGallery))], adminId, ip, ct);

        Status(result.Success ? loc["Admin.Journal.GallerySaved"].Value : Localised(result.Error), isError: !result.Success);
        return RedirectToAction(nameof(Edit), null, new { id, culture = SiteCultures.IsSupported(culture) ? culture : null }, "media-library");
    }

    [HttpPost("{id:guid}/set-cover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCover(Guid id, Guid mediaId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.SetCoverAsync(id, mediaId, adminId, ip, ct);

        Status(result.Success ? loc["Admin.Journal.CoverChanged"].Value : Localised(result.Error), isError: !result.Success);
        return RedirectToAction(nameof(Edit), null, new { id }, "media-library");
    }

    /// <summary>Uploads a cover and drops the one it replaces — file included.</summary>
    [HttpPost("{id:guid}/upload-cover")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> UploadCover(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            Status(loc["Seminar.Error.MediaEmpty"].Value, isError: true);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();

        await using var stream = file.OpenReadStream();
        var result = await journalService.ReplaceCoverAsync(
            id, new MediaUpload(file.FileName, file.ContentType, file.Length, stream), adminId, ip, ct);

        Status(result.Success ? loc["Admin.Journal.CoverChanged"].Value : Localised(result.Error), isError: !result.Success);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:guid}/remove-cover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCover(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.RemoveCoverAsync(id, adminId, ip, ct);

        Status(result.Success ? loc["Admin.Journal.CoverRemoved"].Value : Localised(result.Error), isError: !result.Success);
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Delete ------------------------------------------------------------------------------------

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await journalService.DeleteAsync(id, adminId, ip, ct);

        Status(result.Success
            ? loc["Admin.Journal.Deleted"].Value
            : loc["Admin.Journal.AlreadyGone"].Value, isError: !result.Success);

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
            PublishCard = BuildPublishCard(post),
            UpdatedAtMs = (post.UpdatedAt ?? post.CreatedAt).ToUnixTimeMilliseconds(),
            PublicUrlBase = $"{Request.Scheme}://{Request.Host}/journal/",
        };
    }

    /// <summary>
    /// The post's state in words, and what is still missing. Required: the English headline and
    /// article (JournalService refuses to publish without them, because every language falls back to
    /// English). Everything else is advice: a standfirst, a cover with a description, search text,
    /// the other languages, and enough words to be worth a reader's click.
    /// </summary>
    private PublishCardViewModel BuildPublishCard(JournalPost post)
    {
        var english = JournalContent.Find(post, SiteCultures.Default);
        var englishUrl = Url.Action(nameof(Edit), new { id = post.Id, culture = SiteCultures.Default });
        var words = JournalService.CountWords(english?.Body);
        var hasCover = post.CoverMediaId is not null || !string.IsNullOrWhiteSpace(post.CoverImageUrl);
        var translated = SiteCultures.All.Count(c => JournalContent.Find(post, c.Name) is not null);
        var firstMissing = SiteCultures.All.FirstOrDefault(c => JournalContent.Find(post, c.Name) is null)?.Name ?? SiteCultures.Default;

        var checks = new List<PublishCheck>
        {
            new(loc["Admin.Publish.Check.EnglishTitle"].Value, !string.IsNullOrWhiteSpace(english?.Title), true, englishUrl + "#writer"),
            new(loc["Admin.Publish.Check.EnglishBody"].Value, words > 0, true, englishUrl + "#writer"),
            new(loc["Admin.Journal.Check.Length", MinimumWords].Value, words >= MinimumWords, false, englishUrl + "#writer"),
            new(loc["Admin.Journal.Check.Standfirst"].Value, !string.IsNullOrWhiteSpace(english?.Excerpt), false, englishUrl + "#writer"),
            new(loc["Admin.Publish.Check.Cover"].Value, hasCover, false, "#cover"),
        };
        if (hasCover)
            checks.Add(new(loc["Admin.Journal.Check.CoverAlt"].Value, !string.IsNullOrWhiteSpace(post.CoverImageAlt), false, "#cover"));
        checks.Add(new(loc["Admin.Journal.Check.Search"].Value, !string.IsNullOrWhiteSpace(english?.SeoDescription) || !string.IsNullOrWhiteSpace(english?.Excerpt), false, "#seo"));
        checks.Add(new(loc["Admin.Publish.Check.Languages", translated, SiteCultures.All.Count].Value, translated == SiteCultures.All.Count, false,
            Url.Action(nameof(Edit), new { id = post.Id, culture = firstMissing }) + "#writer"));

        return new PublishCardViewModel
        {
            IsPublished = post.Status == JournalPostStatus.Published,
            StatusLabel = loc["Admin.Publish.Status." + post.Status].Value,
            StatusSentence = post.Status == JournalPostStatus.Published
                ? loc["Admin.Publish.Sentence.Live", (post.PublishedAt ?? DateTimeOffset.UtcNow).ToString("d MMMM yyyy")].Value
                : loc["Admin.Publish.Sentence.Draft"].Value,
            Checks = checks,
            PreviewUrl = Url.Action("Details", "Journal", new { area = "", slug = post.Slug }),
            WriterFormId = "journal-writer",
        };
    }

    /// <summary>Below this the checklist suggests writing more. Advice only; it does not block.</summary>
    private const int MinimumWords = 150;

    /// <summary>Service errors arrive as SharedResource keys, not sentences, so the panel speaks
    /// whichever language the admin set — same contract as AdminSeminarsController.</summary>
    private string Localised(string? key) =>
        string.IsNullOrWhiteSpace(key) ? loc["Admin.Journal.SaveFailed"].Value : loc[key].Value;
}
