using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

/// <summary>
/// BookingReference is the human-readable, customer-facing identifier (e.g. "VI-26-1042") used in
/// URLs, receipts and support conversations — the internal Guid Id is never shown to a customer
/// (brief §110/§188).
/// </summary>
public class Booking : BaseEntity
{
    public string BookingReference { get; set; } = default!;

    public Guid UserId { get; set; }
    public Guid ExperienceId { get; set; }
    public Guid TicketTypeId { get; set; }
    public Guid? ApplicationId { get; set; }

    public int Quantity { get; set; } = 1;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
