using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminBookingDetailViewModel
{
    public Booking Booking { get; set; } = default!;
    public string ExperienceLabel { get; set; } = default!;
    public string? CustomerEmail { get; set; }
    public Payment? Payment { get; set; }
}
