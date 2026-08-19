using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>Read-only mirror of Payment rows — refunds/disputes/manual capture stay in the Stripe
/// dashboard itself once live keys are in place; this screen exists so Finance/Support can see
/// payment status without database access.</summary>
public class AdminPaymentsController(
    IPaymentRepository payments,
    IBookingRepository bookings,
    IExperienceService experienceService,
    IPaymentProvider paymentProvider,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public async Task<IActionResult> Index(PaymentStatus? status, CancellationToken ct)
    {
        var all = await payments.GetAllAsync(ct);
        var filtered = status is null ? all : all.Where(p => p.Status == status).ToList();
        var experiences = await experienceService.GetAllForAdminAsync(ct);
        var experienceLabels = experiences.ToDictionary(e => e.Id, e => $"The VI House — {e.City}");

        var model = new List<AdminPaymentListItemViewModel>();
        foreach (var p in filtered.OrderByDescending(p => p.CreatedAt))
        {
            var user = p.UserId is { } userId ? await userManager.FindByIdAsync(userId.ToString()) : null;
            model.Add(new AdminPaymentListItemViewModel
            {
                Id = p.Id,
                CustomerEmail = user?.Email,
                ExperienceLabel = experienceLabels.GetValueOrDefault(p.ExperienceId, "—"),
                Status = p.Status,
                AmountMinor = p.AmountMinor,
                Currency = p.Currency,
                CreatedAt = p.CreatedAt,
            });
        }

        ViewData["SelectedStatus"] = status;
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var payment = await payments.GetByIdAsync(id, ct);
        if (payment is null) return NotFound();

        var experience = await experienceService.GetForAdminEditAsync(payment.ExperienceId, ct);
        var user = payment.UserId is { } userId ? await userManager.FindByIdAsync(userId.ToString()) : null;
        var booking = payment.BookingId is { } bookingId ? await bookings.GetByIdAsync(bookingId, ct) : null;
        var liveDetails = await paymentProvider.GetPaymentDetailsAsync(payment.ProviderReference, ct);

        return View(new AdminPaymentDetailViewModel
        {
            Payment = payment,
            ExperienceLabel = experience is null ? "—" : $"The VI House — {experience.City}, {experience.Country}",
            CustomerEmail = user?.Email,
            BookingReference = booking?.BookingReference,
            LiveDetails = liveDetails,
        });
    }
}
