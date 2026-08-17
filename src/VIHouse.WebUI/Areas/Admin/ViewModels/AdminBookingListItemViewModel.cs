using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminBookingListItemViewModel
{
    public Guid Id { get; set; }
    public string BookingReference { get; set; } = default!;
    public string? CustomerEmail { get; set; }
    public string ExperienceLabel { get; set; } = default!;
    public BookingStatus Status { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
