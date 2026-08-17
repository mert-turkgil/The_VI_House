using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminPaymentDetailViewModel
{
    public Payment Payment { get; set; } = default!;
    public string ExperienceLabel { get; set; } = default!;
    public string? CustomerEmail { get; set; }
    public string? BookingReference { get; set; }
}
