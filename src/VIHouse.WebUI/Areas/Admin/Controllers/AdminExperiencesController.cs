using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Notifications;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Membership;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminExperiencesController(
    IExperienceService experienceService,
    INotificationService notificationService,
    IMembershipService membershipService,
    IStringLocalizer<SharedResource> loc,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var experiences = await experienceService.GetAllForAdminAsync(ct);

        var model = experiences
            .OrderByDescending(e => e.StartAtUtc)
            .Select(e => new AdminExperienceListItemViewModel
            {
                Id = e.Id,
                Title = e.Title,
                Slug = e.Slug,
                City = e.City,
                Country = e.Country,
                StartAtUtc = e.StartAtUtc,
                Status = e.Status,
                TicketTypeCount = e.TicketTypes.Count,
            })
            .ToList();

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View(new AdminExperienceFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminExperienceFormViewModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var entity = form.ToEntity();
        var (adminId, ip) = CurrentActor();
        await experienceService.CreateAsync(entity, adminId, ip, ct);

        TempData["StatusMessage"] = $"\"{entity.Title}\" created.";
        return RedirectToAction(nameof(Edit), new { id = entity.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var experience = await experienceService.GetForAdminEditAsync(id, ct);
        if (experience is null) return NotFound();

        var model = new AdminExperienceEditViewModel
        {
            Form = AdminExperienceFormViewModel.FromEntity(experience),
            TicketTypes = experience.TicketTypes.OrderBy(t => t.SortOrder).ToList(),
            Inclusions = experience.Inclusions.OrderBy(i => i.SortOrder).ToList(),
            Faqs = experience.Faqs.OrderBy(f => f.SortOrder).ToList(),
            Gallery = experience.Gallery.OrderBy(g => g.SortOrder).ThenBy(g => g.CreatedAt).ToList(),
            ProgramDays = experience.ProgramDays.OrderBy(d => d.SortOrder).ThenBy(d => d.DayNumber).ToList(),
            MemberAccess = await BuildMemberAccessAsync(id, ct),
            CoverPreviewUrl = CoverUrl(experience),
            HasUploadedCover = experience.CoverImageStorageKey is not null,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, AdminExperienceFormViewModel form, CancellationToken ct)
    {
        form.Id = id;

        if (!ModelState.IsValid)
        {
            var experience = await experienceService.GetForAdminEditAsync(id, ct);
            var model = new AdminExperienceEditViewModel
            {
                Form = form,
                TicketTypes = experience?.TicketTypes.OrderBy(t => t.SortOrder).ToList() ?? [],
                Inclusions = experience?.Inclusions.OrderBy(i => i.SortOrder).ToList() ?? [],
                Faqs = experience?.Faqs.OrderBy(f => f.SortOrder).ToList() ?? [],
            };
            return View(model);
        }

        var (adminId, ip) = CurrentActor();
        await experienceService.UpdateCoreFieldsAsync(form.ToEntity(), adminId, ip, ct);
        TempData["StatusMessage"] = "Changes saved.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var deleted = await experienceService.TryDeleteAsync(id, adminId, ip, ct);
        TempData["StatusMessage"] = deleted
            ? "Experience deleted."
            : "Can't delete — this experience already has applications or bookings against it.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTicketType(AddTicketTypeInputModel input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (adminId, ip) = CurrentActor();
            await experienceService.AddTicketTypeAsync(input.ExperienceId, new TicketType
            {
                Title = input.Title,
                Description = input.Description,
                PriceMinor = input.PriceMinor,
                Currency = input.Currency.ToUpperInvariant(),
                Inventory = input.Inventory,
                MaxQuantityPerOrder = input.MaxQuantityPerOrder,
                PerksText = input.PerksText,
            }, adminId, ip, ct);
        }

        return RedirectToAction(nameof(Edit), new { id = input.ExperienceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTicketType(Guid experienceId, Guid ticketTypeId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var removed = await experienceService.TryRemoveTicketTypeAsync(experienceId, ticketTypeId, adminId, ip, ct);
        if (!removed)
            TempData["StatusMessage"] = "Can't remove — this ticket type already has bookings against it.";

        return RedirectToAction(nameof(Edit), new { id = experienceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddInclusion(AddInclusionInputModel input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (adminId, ip) = CurrentActor();
            await experienceService.AddInclusionAsync(input.ExperienceId, new ExperienceInclusion
            {
                Text = input.Text,
                IsIncluded = input.IsIncluded,
            }, adminId, ip, ct);
        }

        return RedirectToAction(nameof(Edit), new { id = input.ExperienceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveInclusion(Guid experienceId, Guid inclusionId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.RemoveInclusionAsync(experienceId, inclusionId, adminId, ip, ct);
        return RedirectToAction(nameof(Edit), new { id = experienceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFaq(AddFaqInputModel input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (adminId, ip) = CurrentActor();
            await experienceService.AddFaqAsync(input.ExperienceId, new ExperienceFaq
            {
                Question = input.Question,
                Answer = input.Answer,
            }, adminId, ip, ct);
        }

        return RedirectToAction(nameof(Edit), new { id = input.ExperienceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFaq(Guid experienceId, Guid faqId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.RemoveFaqAsync(experienceId, faqId, adminId, ip, ct);
        return RedirectToAction(nameof(Edit), new { id = experienceId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NotifyAttendees(NotifyAttendeesInputModel input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (adminId, ip) = CurrentActor();
            var count = await notificationService.BroadcastToExperienceAttendeesAsync(
                input.ExperienceId, input.Type, input.Title, input.Body,
                link: null, adminId, ip, ct);
            TempData["StatusMessage"] = count switch
            {
                0 => "No confirmed attendees to notify yet.",
                1 => "Notified 1 attendee.",
                _ => $"Notified {count} attendees.",
            };
        }

        return RedirectToAction(nameof(Edit), new { id = input.ExperienceId });
    }

    // --- Cover image --------------------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> UploadCover(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["StatusMessage"] = loc["Admin.Experience.NoFile"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();
        await using var stream = file.OpenReadStream();
        var error = await experienceService.UploadCoverAsync(
            id, new MediaUpload(file.FileName, file.ContentType, file.Length, stream), adminId, ip, ct);

        TempData["StatusMessage"] = loc[error ?? "Admin.Experience.CoverUploaded"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCover(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.RemoveCoverAsync(id, adminId, ip, ct);
        TempData["StatusMessage"] = loc["Admin.Experience.CoverRemoved"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Gallery ------------------------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> AddGalleryImage(Guid id, IFormFile? file, string? url, string? altText, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        string? error;

        // An upload and a typed path are both accepted, because the seeded gallery is all committed
        // paths and there is no reason to force those to be re-uploaded to edit a caption.
        if (file is { Length: > 0 })
        {
            await using var stream = file.OpenReadStream();
            error = await experienceService.AddGalleryImageAsync(
                id, new MediaUpload(file.FileName, file.ContentType, file.Length, stream), altText, adminId, ip, ct);
        }
        else if (!string.IsNullOrWhiteSpace(url))
        {
            error = await experienceService.AddGalleryImageByUrlAsync(id, url.Trim(), altText, adminId, ip, ct);
        }
        else
        {
            error = "Admin.Experience.NoFile";
        }

        TempData["StatusMessage"] = loc[error ?? "Admin.Experience.GalleryAdded"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGalleryImage(Guid id, Guid imageId, string? altText, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.UpdateGalleryImageAsync(id, imageId, altText, adminId, ip, ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGalleryImage(Guid id, Guid imageId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.RemoveGalleryImageAsync(id, imageId, adminId, ip, ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveGalleryImage(Guid id, Guid imageId, int delta, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.MoveGalleryImageAsync(id, imageId, delta, adminId, ip, ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Programme ----------------------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProgramDay(Guid id, int dayNumber, string title, string? dateLabel, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            var (adminId, ip) = CurrentActor();
            await experienceService.AddProgramDayAsync(id, new ExperienceProgramDay
            {
                DayNumber = dayNumber,
                Title = title.Trim(),
                DateLabel = dateLabel,
                SortOrder = dayNumber,
            }, adminId, ip, ct);
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProgramDay(Guid id, Guid dayId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.RemoveProgramDayAsync(id, dayId, adminId, ip, ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSession(
        Guid id, Guid dayId, string title, TimeSpan startTime, TimeSpan endTime,
        string? speakerName, string? description, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            var (adminId, ip) = CurrentActor();
            var error = await experienceService.AddSessionAsync(id, dayId, new ExperienceSession
            {
                Title = title.Trim(),
                StartTime = startTime,
                EndTime = endTime,
                SpeakerName = speakerName,
                Description = description,
            }, adminId, ip, ct);

            if (error is not null) TempData["StatusMessage"] = loc[error].Value;
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSession(Guid id, Guid sessionId, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.RemoveSessionAsync(id, sessionId, adminId, ip, ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    // --- Editing existing child rows ------------------------------------------------------------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTicketType(Guid id, TicketType input, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var error = await experienceService.UpdateTicketTypeAsync(id, input, adminId, ip, ct);
        if (error is not null) TempData["StatusMessage"] = loc[error].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInclusion(Guid id, Guid inclusionId, string text, bool isIncluded, int sortOrder, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var error = await experienceService.UpdateInclusionAsync(id, inclusionId, text, isIncluded, sortOrder, adminId, ip, ct);
        if (error is not null) TempData["StatusMessage"] = loc[error].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFaq(Guid id, Guid faqId, string question, string answer, int sortOrder, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var error = await experienceService.UpdateFaqAsync(id, faqId, question, answer, sortOrder, adminId, ip, ct);
        if (error is not null) TempData["StatusMessage"] = loc[error].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMemberAccess(Guid id, Guid[]? planIds, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await experienceService.SetMembershipAccessAsync(id, planIds ?? [], adminId, ip, ct);
        TempData["StatusMessage"] = loc["Admin.Experience.MemberAccessSaved"].Value;
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>
    /// Every plan that exists, flagged with whether this experience admits it. Archived plans are
    /// included when already ticked, so an experience does not silently lose access it was granted.
    /// </summary>
    private async Task<List<ExperienceMemberAccessOption>> BuildMemberAccessAsync(Guid experienceId, CancellationToken ct)
    {
        var admitted = (await experienceService.GetMembershipAccessAsync(experienceId, ct)).ToHashSet();
        var plans = await membershipService.GetAllPlansAsync(ct);

        return [.. plans
            .Where(p => p.Status == MembershipPlanStatus.Active || admitted.Contains(p.Id))
            .OrderBy(p => p.SortOrder)
            .Select(p => new ExperienceMemberAccessOption(
                p.Id, p.Name, MoneyFormatter.Format(p.PriceMinor, p.Currency), admitted.Contains(p.Id)))];
    }

    /// <summary>
    /// Where the cover comes from. An uploaded file is streamed by MediaController and stamped with
    /// the row's UpdatedAt, so replacing it produces a new URL rather than waiting out a cached one.
    /// </summary>
    private string? CoverUrl(Experience experience) =>
        experience.CoverImageStorageKey is null
            ? experience.CoverImageUrl
            : Url.Action("ExperienceCover", "Media", new { area = "", id = experience.Id, v = (experience.UpdatedAt ?? experience.CreatedAt).ToUnixTimeSeconds() });

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
