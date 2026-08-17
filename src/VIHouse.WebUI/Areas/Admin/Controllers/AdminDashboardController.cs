using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminDashboardController(
    IApplicationRepository applications,
    IBookingRepository bookings,
    IPaymentRepository payments) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var submitted = await applications.GetByStatusAsync(ApplicationStatus.Submitted, ct);
        var underReview = await applications.GetByStatusAsync(ApplicationStatus.UnderReview, ct);
        var allBookings = await bookings.GetAllAsync(ct);
        var allPayments = await payments.GetAllAsync(ct);

        var applicationsByStatus = new Dictionary<ApplicationStatus, int>();
        var totalApplications = 0;
        foreach (ApplicationStatus status in Enum.GetValues<ApplicationStatus>())
        {
            var count = (await applications.GetByStatusAsync(status, ct)).Count;
            applicationsByStatus[status] = count;
            totalApplications += count;
        }

        var paidCount = applicationsByStatus.GetValueOrDefault(ApplicationStatus.Paid);

        var model = new AdminDashboardViewModel
        {
            PendingApplications = submitted.Count + underReview.Count,
            TotalBookings = allBookings.Count,
            TotalApplications = totalApplications,
            ApplicationsByStatus = applicationsByStatus,
            ConversionToPaidPercent = totalApplications == 0 ? 0 : Math.Round(paidCount * 100.0 / totalApplications, 1),
            RevenueByCurrency = allPayments
                .Where(p => p.Status == PaymentStatus.Paid)
                .GroupBy(p => p.Currency)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountMinor)),
            BookingsByStatus = allBookings
                .GroupBy(b => b.Status)
                .ToDictionary(g => g.Key, g => g.Count()),
        };

        return View(model);
    }
}
