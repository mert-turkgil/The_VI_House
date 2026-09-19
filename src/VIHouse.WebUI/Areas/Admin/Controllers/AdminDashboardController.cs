using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Every figure on the dashboard is a query: applications and bookings from their own tables,
/// everything about money from <see cref="IPaymentReportingService"/> (the unified
/// PaymentTransactions view), so a webhook that moves a payment moves the dashboard on the next
/// load. Money figures are shown only to the roles that may see them (RolesFor.Money); the rest
/// of the page is for everyone with admin access.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Everyone)]
[Route("admin")]
public class AdminDashboardController(
    IApplicationRepository applications,
    IBookingRepository bookings,
    IPaymentReportingService reporting) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var allApplications = await applications.GetAllAsync(ct);
        var applicationsByStatus = allApplications.GroupBy(a => a.Status).ToDictionary(g => g.Key, g => g.Count());
        var totalApplications = allApplications.Count;
        var paidCount = applicationsByStatus.GetValueOrDefault(ApplicationStatus.Paid);

        var allBookings = await bookings.GetAllAsync(ct);

        var canSeeMoney = AdminSections.RolesFor.Money.Split(',').Any(User.IsInRole);
        var payments = canSeeMoney ? await reporting.GetDashboardStatsAsync(ct) : null;

        var model = new AdminDashboardViewModel
        {
            PendingApplications = applicationsByStatus.GetValueOrDefault(ApplicationStatus.Submitted) + applicationsByStatus.GetValueOrDefault(ApplicationStatus.UnderReview),
            TotalBookings = allBookings.Count,
            TotalApplications = totalApplications,
            ApplicationsByStatus = applicationsByStatus,
            ConversionToPaidPercent = totalApplications == 0 ? 0 : Math.Round(paidCount * 100.0 / totalApplications, 1),
            BookingsByStatus = allBookings.GroupBy(b => b.Status).ToDictionary(g => g.Key, g => g.Count()),
            CanSeeMoney = canSeeMoney,
            Payments = payments,
        };

        return View(model);
    }
}
