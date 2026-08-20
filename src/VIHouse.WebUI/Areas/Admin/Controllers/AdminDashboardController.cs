using Microsoft.AspNetCore.Mvc;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

public class AdminDashboardController(
    IApplicationRepository applications,
    IBookingRepository bookings,
    IPaymentRepository payments,
    IMembershipPaymentRepository membershipPayments) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var submitted = await applications.GetByStatusAsync(ApplicationStatus.Submitted, ct);
        var underReview = await applications.GetByStatusAsync(ApplicationStatus.UnderReview, ct);
        var allBookings = await bookings.GetAllAsync(ct);
        var allPayments = await payments.GetAllAsync(ct);
        var allMembershipPayments = await membershipPayments.GetAllAsync(ct);

        var applicationsByStatus = new Dictionary<ApplicationStatus, int>();
        var totalApplications = 0;
        foreach (ApplicationStatus status in Enum.GetValues<ApplicationStatus>())
        {
            var count = (await applications.GetByStatusAsync(status, ct)).Count;
            applicationsByStatus[status] = count;
            totalApplications += count;
        }

        var paidCount = applicationsByStatus.GetValueOrDefault(ApplicationStatus.Paid);

        // Experience tickets and standalone memberships live in two different tables (see
        // MembershipPayment's doc comment for why), but they're the same money — reporting only one
        // of them understates revenue, which is how membership income was previously invisible here.
        var paidPayments = allPayments
            .Where(p => p.Status == PaymentStatus.Paid)
            .Select(p => (p.Currency, p.AmountMinor, p.CreatedAt))
            .Concat(allMembershipPayments
                .Where(p => p.Status == PaymentStatus.Paid)
                .Select(p => (p.Currency, p.AmountMinor, p.CreatedAt)))
            .ToList();

        // The house takes payment in more than one currency, and adding EUR to GBP would produce a
        // number that means nothing. The trend line therefore tracks whichever currency carries the
        // most revenue, labelled as such, rather than a misleading combined total.
        var primaryCurrency = paidPayments
            .GroupBy(p => p.Currency)
            .OrderByDescending(g => g.Sum(p => p.AmountMinor))
            .FirstOrDefault()?.Key;

        var revenueByMonth = new List<MonthlyRevenuePoint>();
        if (primaryCurrency is not null)
        {
            var thisMonth = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var inPrimaryCurrency = paidPayments.Where(p => p.Currency == primaryCurrency).ToList();

            for (var offset = 11; offset >= 0; offset--)
            {
                var monthStart = thisMonth.AddMonths(-offset);
                var monthEnd = monthStart.AddMonths(1);
                var total = inPrimaryCurrency
                    .Where(p => p.CreatedAt >= monthStart && p.CreatedAt < monthEnd)
                    .Sum(p => p.AmountMinor);

                revenueByMonth.Add(new MonthlyRevenuePoint(monthStart.ToString("MMM yy"), total));
            }
        }

        var model = new AdminDashboardViewModel
        {
            PendingApplications = submitted.Count + underReview.Count,
            TotalBookings = allBookings.Count,
            TotalApplications = totalApplications,
            ApplicationsByStatus = applicationsByStatus,
            ConversionToPaidPercent = totalApplications == 0 ? 0 : Math.Round(paidCount * 100.0 / totalApplications, 1),
            RevenueByCurrency = paidPayments
                .GroupBy(p => p.Currency)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountMinor)),
            RevenueByMonth = revenueByMonth,
            PrimaryCurrency = primaryCurrency,
            BookingsByStatus = allBookings
                .GroupBy(b => b.Status)
                .ToDictionary(g => g.Key, g => g.Count()),
        };

        return View(model);
    }
}
