using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminDashboardViewModel
{
    public int PendingApplications { get; set; }
    public int TotalBookings { get; set; }

    // --- Analytics / conversion tracking (brief §206 "Analytics" bullet) ------------------------
    public int TotalApplications { get; set; }
    public Dictionary<ApplicationStatus, int> ApplicationsByStatus { get; set; } = [];
    public double ConversionToPaidPercent { get; set; }
    public Dictionary<string, long> RevenueByCurrency { get; set; } = [];
    public Dictionary<BookingStatus, int> BookingsByStatus { get; set; } = [];

    /// <summary>Collected revenue per calendar month for the trailing 12 months, in the currency
    /// most of it was taken in. Months with no payments are present with a zero so the chart shows a
    /// continuous timeline rather than skipping quiet periods.</summary>
    public List<MonthlyRevenuePoint> RevenueByMonth { get; set; } = [];

    /// <summary>Currency the monthly series is denominated in — null when nothing has been paid yet.
    /// Mixed-currency revenue is deliberately not summed; see AdminDashboardController.</summary>
    public string? PrimaryCurrency { get; set; }
}

public record MonthlyRevenuePoint(string Label, long AmountMinor);
