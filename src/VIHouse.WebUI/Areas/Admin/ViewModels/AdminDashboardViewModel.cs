using VIHouse.Business.Abstract;
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
    public Dictionary<BookingStatus, int> BookingsByStatus { get; set; } = [];

    /// <summary>Whether the signed-in admin may see money at all (RolesFor.Money). When false,
    /// <see cref="Payments"/> is null and the page shows no revenue or payment figures.</summary>
    public bool CanSeeMoney { get; set; }

    /// <summary>Everything about money, per currency, from the unified transaction table.</summary>
    public PaymentDashboardStats? Payments { get; set; }
}
