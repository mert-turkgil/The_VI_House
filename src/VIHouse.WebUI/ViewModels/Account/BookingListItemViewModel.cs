using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.ViewModels.Account;

public class BookingListItemViewModel
{
    public string BookingReference { get; set; } = default!;
    public string ExperienceLabel { get; set; } = default!;
    public DateTimeOffset StartAtUtc { get; set; }
    public BookingStatus Status { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = default!;
}
