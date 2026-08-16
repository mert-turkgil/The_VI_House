using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminDashboardController(IApplicationRepository applications, IBookingRepository bookings) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Real KPI wiring (revenue, capacity, funnel) lands with the Applications/Bookings admin
        // screens in a later milestone — this proves the admin shell + auth + 2FA gate end-to-end
        // with the two counts already backed by real repositories.
        var submitted = await applications.GetByStatusAsync(ApplicationStatus.Submitted, ct);
        var underReview = await applications.GetByStatusAsync(ApplicationStatus.UnderReview, ct);
        var allBookings = await bookings.GetAllAsync(ct);

        var model = new AdminDashboardViewModel
        {
            PendingApplications = submitted.Count + underReview.Count,
            TotalBookings = allBookings.Count,
        };

        return View(model);
    }
}
