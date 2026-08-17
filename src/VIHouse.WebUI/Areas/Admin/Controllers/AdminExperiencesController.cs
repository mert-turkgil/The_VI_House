using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Experiences;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminExperiencesController(IExperienceService experienceService, UserManager<ApplicationUser> userManager) : AdminControllerBase
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

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());
}
