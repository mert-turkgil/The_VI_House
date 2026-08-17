using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminBookingsController(
    IBookingRepository bookings,
    IPaymentRepository payments,
    IExperienceService experienceService,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(BookingStatus? status, CancellationToken ct)
    {
        var all = await bookings.GetAllAsync(ct);
        var filtered = status is null ? all : all.Where(b => b.Status == status).ToList();
        var experiences = await experienceService.GetAllForAdminAsync(ct);
        var experienceLabels = experiences.ToDictionary(e => e.Id, e => $"The VI House — {e.City}");

        var model = new List<AdminBookingListItemViewModel>();
        foreach (var b in filtered.OrderByDescending(b => b.CreatedAt))
        {
            var user = await userManager.FindByIdAsync(b.UserId.ToString());
            model.Add(new AdminBookingListItemViewModel
            {
                Id = b.Id,
                BookingReference = b.BookingReference,
                CustomerEmail = user?.Email,
                ExperienceLabel = experienceLabels.GetValueOrDefault(b.ExperienceId, "—"),
                Status = b.Status,
                AmountMinor = b.AmountMinor,
                Currency = b.Currency,
                CreatedAt = b.CreatedAt,
            });
        }

        ViewData["SelectedStatus"] = status;
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var booking = await bookings.GetByIdAsync(id, ct);
        if (booking is null) return NotFound();

        var experience = await experienceService.GetForAdminEditAsync(booking.ExperienceId, ct);
        var user = await userManager.FindByIdAsync(booking.UserId.ToString());
        var payment = booking.ApplicationId is { } applicationId
            ? (await payments.GetByApplicationAsync(applicationId, ct)).FirstOrDefault(p => p.BookingId == booking.Id)
            : null;

        return View(new AdminBookingDetailViewModel
        {
            Booking = booking,
            ExperienceLabel = experience is null ? "—" : $"The VI House — {experience.City}, {experience.Country}",
            CustomerEmail = user?.Email,
            Payment = payment,
        });
    }
}
