using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.ViewModels.Account;

/// <summary>One booking as a card on the account's Experiences page.</summary>
public class BookingListItemViewModel
{
    public string BookingReference { get; set; } = default!;
    public string ExperienceLabel { get; set; } = default!;
    public string? Slug { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public DateTimeOffset StartAtUtc { get; set; }
    public DateTimeOffset? EndAtUtc { get; set; }
    public BookingStatus Status { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = default!;

    /// <summary>Over once the experience has ended; a cancelled or refunded booking is past too.</summary>
    public bool IsPast => (EndAtUtc ?? StartAtUtc) < DateTimeOffset.UtcNow || Status is BookingStatus.Cancelled or BookingStatus.Refunded;
}
