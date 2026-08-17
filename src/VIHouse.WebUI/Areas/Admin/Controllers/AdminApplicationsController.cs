using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Applications;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminApplicationsController(
    IApplicationService applicationService,
    IExperienceService experienceService,
    IInvitationRepository invitations,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(ApplicationStatus? status, CancellationToken ct)
    {
        var applications = await applicationService.GetByStatusAsync(status, ct);
        var experiences = await experienceService.GetAllForAdminAsync(ct);
        var experienceLabels = experiences.ToDictionary(e => e.Id, e => $"{e.City}, {e.Country}");

        var model = applications
            .OrderByDescending(a => a.SubmittedAt)
            .Select(a => new AdminApplicationListItemViewModel
            {
                Id = a.Id,
                FirstName = a.FirstName,
                LastName = a.LastName,
                Email = a.Email,
                ExperienceLabel = experienceLabels.GetValueOrDefault(a.ExperienceId, "—"),
                Status = a.Status,
                SubmittedAt = a.SubmittedAt,
            })
            .ToList();

        ViewData["SelectedStatus"] = status;
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var application = await applicationService.GetForAdminAsync(id, ct);
        if (application is null) return NotFound();

        var experience = await experienceService.GetForAdminEditAsync(application.ExperienceId, ct);
        var reviewer = application.ReviewedByUserId is { } reviewerId
            ? await userManager.FindByIdAsync(reviewerId.ToString())
            : null;
        var invitation = await invitations.GetLatestByApplicationAsync(id, ct);

        var model = new AdminApplicationDetailViewModel
        {
            Application = application,
            ExperienceLabel = experience is null ? "—" : $"The VI House — {experience.City}, {experience.Country}",
            ReviewedByEmail = reviewer?.Email,
            Invitation = invitation,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkUnderReview(Guid id, CancellationToken ct)
    {
        await RunTransitionAsync(id, (adminId, ip, c) => applicationService.MarkUnderReviewAsync(id, adminId, ip, c), ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Shortlist(Guid id, CancellationToken ct)
    {
        await RunTransitionAsync(id, (adminId, ip, c) => applicationService.ShortlistAsync(id, adminId, ip, c), ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        await RunTransitionAsync(id, (adminId, ip, c) => applicationService.ApproveAsync(id, adminId, ip, c), ct);
        TempData["StatusMessage"] = "Application approved — an invitation has been issued.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string? reason, CancellationToken ct)
    {
        await RunTransitionAsync(id, (adminId, ip, c) => applicationService.RejectAsync(id, adminId, reason, ip, c), ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Waitlist(Guid id, CancellationToken ct)
    {
        await RunTransitionAsync(id, (adminId, ip, c) => applicationService.WaitlistAsync(id, adminId, ip, c), ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateNotes(Guid id, string? internalNotes, CancellationToken ct)
    {
        await applicationService.UpdateInternalNotesAsync(id, internalNotes, ct);
        TempData["StatusMessage"] = "Notes saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTag(Guid id, string label, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(label))
            await applicationService.AddTagAsync(id, label.Trim(), ct);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTag(Guid id, Guid tagId, CancellationToken ct)
    {
        await applicationService.RemoveTagAsync(id, tagId, ct);
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task RunTransitionAsync(Guid id, Func<Guid, string?, CancellationToken, Task> action, CancellationToken ct)
    {
        var adminId = Guid.Parse(userManager.GetUserId(User)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        try
        {
            await action(adminId, ip, ct);
        }
        catch (InvalidOperationException ex)
        {
            TempData["StatusMessage"] = ex.Message;
        }
    }
}
