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
}
