using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.ViewModels.Account;

/// <summary>
/// The thing a single-event attendee actually turns up with. Distinct from
/// <see cref="BookingListItemViewModel"/>, which is just a row in a list — this is the ticket
/// itself, built to be shown on a phone at the door.
/// </summary>
public class TicketViewModel
{
    public string BookingReference { get; set; } = default!;
    public string HolderName { get; set; } = default!;

    public string ExperienceTitle { get; set; } = default!;
    public string City { get; set; } = default!;
    public string Country { get; set; } = default!;
    public DateTimeOffset StartAtUtc { get; set; }
    public DateTimeOffset EndAtUtc { get; set; }

    public string? TicketTypeName { get; set; }
    public int Quantity { get; set; } = 1;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = default!;

    public BookingStatus Status { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>True when the holder also has a live membership — used to explain, on the ticket
    /// itself, that a ticket alone doesn't include the year-round community.</summary>
    public bool HolderIsMember { get; set; }

    public bool IsPast => EndAtUtc < DateTimeOffset.UtcNow;
}
