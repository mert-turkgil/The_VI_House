using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Content;
using VIHouse.Entities.Seminars;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// The homepage hero carousel: add a panel, give it a photograph and a heading in each of the four
/// languages, order them, publish.
///
/// Split into independent forms the way the seminar editor is — the slide's own fields, the image,
/// and one form per language each post to their own action. A single bound form would mean that
/// re-ordering the slides re-posts, and can therefore silently clobber, copy someone is halfway
/// through writing in another tab.
///
/// Ordinary content editing, so it inherits the base class's role list rather than narrowing it.
/// </summary>
public class AdminHeroSlidesController(
    IHeroSlideRepository slides,
    IMediaStorage mediaStorage,
    IAuditLogRepository auditLogs,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    /// <summary>Where uploaded hero photography is stored, under the media root.</summary>
    private const string StorageFolder = "hero";

    // --- Index ------------------------------------------------------------------------------------

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var all = await slides.GetAllWithTranslationsAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var model = all.Select(s => new AdminHeroSlideListItemViewModel
        {
            Id = s.Id,
            Heading = HeroSlideContent.Heading(s, SiteCultures.Default),
            SortOrder = s.SortOrder,
            IsActive = s.IsActive,
            ImagePreviewUrl = PreviewUrl(s),
            VisibleFromUtc = s.VisibleFromUtc,
            VisibleUntilUtc = s.VisibleUntilUtc,
            TranslatedCultures = [.. s.Translations.Select(t => t.Culture).Order()],
            IsLiveNow = s.IsActive
                && (s.VisibleFromUtc is null || s.VisibleFromUtc <= now)
                && (s.VisibleUntilUtc is null || s.VisibleUntilUtc >= now),
        }).ToList();

        return View(model);
    }

    // --- Create ------------------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View(new AdminHeroSlideCreateViewModel
        {
            Form = new AdminHeroSlideFormViewModel { SortOrder = await slides.GetNextSortOrderAsync(ct) },
        });

    /// <summary>
    /// A slide and its default-language heading are created together: a slide with no copy in any
    /// language is not something the homepage can render, and HeroSlideContent would skip it. The
    /// other three languages are filled in on the edit screen afterwards.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    // The parameter is `model`, not `form`: a parameter whose name matches a property on the bound
    // type (AdminHeroSlideCreateViewModel.Form) changes how the binder reads the request and warns
    // about it — MVC1004.
    public async Task<IActionResult> Create(AdminHeroSlideCreateViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var slide = new HeroSlide();
        model.Form.ApplyTo(slide);

        var translation = new HeroSlideTranslation { HeroSlideId = slide.Id, Culture = SiteCultures.Default };
        model.DefaultTranslation.Culture = SiteCultures.Default;
        model.DefaultTranslation.ApplyTo(translation);
        slide.Translations.Add(translation);

        var (adminId, ip) = CurrentActor();
        await slides.AddAsync(slide, ct);
        await LogAsync("HeroSlideCreated", slide.Id, adminId, ip,
            before: null, after: new { translation.Heading, slide.SortOrder, slide.IsActive }, ct);
        await slides.SaveChangesAsync(ct);

        TempData["StatusMessage"] = loc["Admin.HeroSlide.Created", translation.Heading].Value;
        return RedirectToAction(nameof(Edit), new { id = slide.Id });
    }

    // --- Edit ------------------------------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, string? culture, CancellationToken ct)
    {
        var slide = await slides.GetByIdWithTranslationsAsync(id, ct);
        if (slide is null) return NotFound();

        return View(BuildEditModel(slide, culture));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Guid id, AdminHeroSlideFormViewModel form, CancellationToken ct)
    {
        var slide = await slides.GetByIdWithTranslationsAsync(id, ct);
        if (slide is null) return NotFound();

        if (!ModelState.IsValid)
        {
            var invalid = BuildEditModel(slide, SiteCultures.Default);
            invalid.Form = form;
            return View(nameof(Edit), invalid);
        }

        var before = new { slide.ImageUrl, slide.SortOrder, slide.IsActive, slide.PrimaryCtaUrl, slide.SecondaryCtaUrl };

        form.ApplyTo(slide);
        slide.UpdatedAt = DateTimeOffset.UtcNow;

        var (adminId, ip) = CurrentActor();
        await LogAsync("HeroSlideUpdated", slide.Id, adminId, ip, before,
            new { slide.ImageUrl, slide.SortOrder, slide.IsActive, slide.PrimaryCtaUrl, slide.SecondaryCtaUrl }, ct);
        await slides.SaveChangesAsync(ct);

        TempData["StatusMessage"] = loc["Admin.HeroSlide.Saved"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Translations ------------------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTranslation(Guid id, AdminHeroSlideTranslationFormViewModel form, CancellationToken ct)
    {
        var slide = await slides.GetByIdWithTranslationsAsync(id, ct);
        if (slide is null) return NotFound();

        // The culture is posted, so it can be anything. Anything not on the site's list is refused
        // rather than written: a hand-edited form would otherwise create an "xx-XX" row that no
        // reader can be served and no tab can edit.
        if (!SiteCultures.IsSupported(form.Culture))
        {
            TempData["StatusMessage"] = loc["Admin.HeroSlide.UnknownCulture"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (!ModelState.IsValid)
        {
            var invalid = BuildEditModel(slide, form.Culture);
            invalid.Translations = [.. invalid.Translations.Select(tab =>
                tab.Culture.Name == form.Culture ? tab with { Form = form } : tab)];
            return View(nameof(Edit), invalid);
        }

        var existing = HeroSlideContent.Find(slide, form.Culture);
        if (existing is null)
        {
            existing = new HeroSlideTranslation { HeroSlideId = slide.Id, Culture = SiteCultures.Normalise(form.Culture) };
            slide.Translations.Add(existing);
        }
        else
        {
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        form.ApplyTo(existing);

        var (adminId, ip) = CurrentActor();
        await LogAsync("HeroSlideTranslationSaved", slide.Id, adminId, ip,
            before: null, after: new { existing.Culture, existing.Heading }, ct);
        await slides.SaveChangesAsync(ct);

        TempData["StatusMessage"] = loc["Admin.HeroSlide.TranslationSaved", SiteCultures.Describe(form.Culture).NativeLabel].Value;
        return RedirectToAction(nameof(Edit), new { id, culture = form.Culture });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTranslation(Guid id, string culture, CancellationToken ct)
    {
        var slide = await slides.GetByIdWithTranslationsAsync(id, ct);
        if (slide is null) return NotFound();

        // The default culture is what every other language falls back to, so removing it would
        // leave a slide that renders in no language correctly.
        if (string.Equals(culture, SiteCultures.Default, StringComparison.OrdinalIgnoreCase))
        {
            TempData["StatusMessage"] = loc["Admin.HeroSlide.CannotDeleteDefault"].Value;
            return RedirectToAction(nameof(Edit), new { id, culture });
        }

        if (HeroSlideContent.Find(slide, culture) is { } translation)
        {
            slide.Translations.Remove(translation);

            var (adminId, ip) = CurrentActor();
            await LogAsync("HeroSlideTranslationDeleted", slide.Id, adminId, ip,
                before: new { translation.Culture, translation.Heading }, after: null, ct);
            await slides.SaveChangesAsync(ct);

            TempData["StatusMessage"] = loc["Admin.HeroSlide.TranslationDeleted", SiteCultures.Describe(culture).NativeLabel].Value;
        }

        return RedirectToAction(nameof(Edit), new { id, culture });
    }

    // --- Image ------------------------------------------------------------------------------------

    /// <summary>
    /// Stores an uploaded photograph and points the slide at it. Uploads go to the media root
    /// rather than wwwroot — see MediaController — and are streamed back out from there.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile? file, CancellationToken ct)
    {
        var slide = await slides.GetByIdWithTranslationsAsync(id, ct);
        if (slide is null) return NotFound();

        if (file is null || file.Length == 0)
        {
            TempData["StatusMessage"] = loc["Seminar.Error.MediaEmpty"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        // A hero is a photograph. The storage allow-list also permits video and PDF, which would be
        // stored happily and then rendered into an <img> that shows nothing.
        if (MediaPolicy.Classify(file.FileName) is not SeminarMediaKind.Image)
        {
            TempData["StatusMessage"] = loc["Admin.HeroSlide.ImageTypeOnly"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        await using var stream = file.OpenReadStream();
        var saved = await mediaStorage.SaveAsync(
            new MediaUpload(file.FileName, file.ContentType, file.Length, stream), StorageFolder, ct);

        if (!saved.Success)
        {
            TempData["StatusMessage"] = loc[saved.Error ?? "Seminar.Error.MediaFailed"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        var previous = slide.ImageStorageKey;
        slide.ImageStorageKey = saved.StorageKey;
        // An uploaded image wins outright: keeping both would leave two sources of truth for one
        // img src, and the next person reading this would have to guess which one is showing.
        slide.ImageUrl = null;
        slide.UpdatedAt = DateTimeOffset.UtcNow;

        var (adminId, ip) = CurrentActor();
        await LogAsync("HeroSlideImageUploaded", slide.Id, adminId, ip,
            before: new { StorageKey = previous }, after: new { slide.ImageStorageKey, file.FileName }, ct);
        await slides.SaveChangesAsync(ct);

        // Only after the row is committed: a file deleted before the save would be lost for good if
        // the commit then failed, leaving the slide pointing at nothing.
        if (previous is not null) await mediaStorage.DeleteAsync(previous, ct);

        TempData["StatusMessage"] = loc["Admin.HeroSlide.ImageUploaded"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveImage(Guid id, CancellationToken ct)
    {
        var slide = await slides.GetByIdWithTranslationsAsync(id, ct);
        if (slide is null) return NotFound();

        var previous = slide.ImageStorageKey;
        slide.ImageStorageKey = null;
        slide.ImageUrl = null;
        slide.UpdatedAt = DateTimeOffset.UtcNow;

        var (adminId, ip) = CurrentActor();
        await LogAsync("HeroSlideImageRemoved", slide.Id, adminId, ip,
            before: new { StorageKey = previous }, after: null, ct);
        await slides.SaveChangesAsync(ct);

        if (previous is not null) await mediaStorage.DeleteAsync(previous, ct);

        TempData["StatusMessage"] = loc["Admin.HeroSlide.ImageRemoved"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Ordering and deletion ----------------------------------------------------------------------

    /// <summary>
    /// Swaps a slide with its neighbour. Arrows rather than typed numbers because the numbers only
    /// ever matter relative to each other — and because two slides sharing a position fall back to
    /// ordering by CreatedAt, which is not what the admin who typed them meant.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Move(Guid id, int direction, CancellationToken ct)
    {
        var ordered = await slides.GetAllWithTranslationsAsync(ct);
        var index = ordered.FindIndex(s => s.Id == id);
        if (index < 0) return NotFound();

        var target = index + Math.Sign(direction);
        if (target < 0 || target >= ordered.Count) return RedirectToAction(nameof(Index));

        // Renumbered from the list's order rather than by swapping the two SortOrder values, which
        // does nothing at all when both are 0 — the state every slide starts in.
        (ordered[index], ordered[target]) = (ordered[target], ordered[index]);
        for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;

        var (adminId, ip) = CurrentActor();
        await LogAsync("HeroSlideReordered", id, adminId, ip,
            before: new { From = index }, after: new { To = target }, ct);
        await slides.SaveChangesAsync(ct);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var slide = await slides.GetByIdWithTranslationsAsync(id, ct);
        if (slide is null)
        {
            TempData["StatusMessage"] = loc["Admin.HeroSlide.AlreadyGone"].Value;
            return RedirectToAction(nameof(Index));
        }

        var heading = HeroSlideContent.Heading(slide, SiteCultures.Default);
        var storageKey = slide.ImageStorageKey;

        var (adminId, ip) = CurrentActor();
        slides.Remove(slide);
        await LogAsync("HeroSlideDeleted", slide.Id, adminId, ip,
            before: new { Heading = heading, Languages = slide.Translations.Count }, after: null, ct);
        await slides.SaveChangesAsync(ct);

        if (storageKey is not null) await mediaStorage.DeleteAsync(storageKey, ct);

        TempData["StatusMessage"] = loc["Admin.HeroSlide.Deleted", heading].Value;
        return RedirectToAction(nameof(Index));
    }

    // --- Helpers ------------------------------------------------------------------------------------

    private AdminHeroSlideEditViewModel BuildEditModel(HeroSlide slide, string? culture)
    {
        var active = SiteCultures.IsSupported(culture) ? SiteCultures.Normalise(culture) : SiteCultures.Default;

        return new AdminHeroSlideEditViewModel
        {
            Form = AdminHeroSlideFormViewModel.FromEntity(slide),
            ImagePreviewUrl = PreviewUrl(slide),
            HasUploadedImage = slide.ImageStorageKey is not null,
            ActiveCulture = active,
            // Every supported language gets a tab whether or not it has been written — the screen
            // exists as much to show what is missing as to edit what is there.
            Translations = [.. SiteCultures.All.Select(c =>
            {
                var existing = HeroSlideContent.Find(slide, c.Name);
                return new AdminHeroSlideTranslationTab(
                    c,
                    existing is not null,
                    c.Name == SiteCultures.Default,
                    existing is null
                        ? AdminHeroSlideTranslationFormViewModel.Empty(slide.Id, c.Name)
                        : AdminHeroSlideTranslationFormViewModel.FromEntity(slide.Id, existing));
            })],
        };
    }

    /// <summary>Mirrors HomeController.HeroImageUrl so the panel previews exactly what the homepage
    /// renders, version stamp included.</summary>
    private string? PreviewUrl(HeroSlide slide) =>
        slide.ImageStorageKey is null
            ? slide.ImageUrl
            : Url.Action("HeroImage", "Media", new { area = "", id = slide.Id, v = (slide.UpdatedAt ?? slide.CreatedAt).ToUnixTimeSeconds() });

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(HeroSlide),
            EntityId = entityId,
            DataBefore = before is null ? null : System.Text.Json.JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : System.Text.Json.JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
