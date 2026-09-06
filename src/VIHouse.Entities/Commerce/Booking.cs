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

    /// <summary>
    /// Null for a booking that never went through checkout — a member joining on their membership
    /// buys no ticket, so there is no ticket type and no inventory to move.
    /// </summary>
    public Guid? TicketTypeId { get; set; }

    /// <summary>Null for the same reason: a membership booking has no application behind it.</summary>
    public Guid? ApplicationId { get; set; }

    public int Quantity { get; set; } = 1;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    /// <summary>
    /// Why this booking exists. Defaults to Purchase, which is what every row written before
    /// memberships could join an experience actually was.
    /// </summary>
    public BookingGrant GrantedVia { get; set; } = BookingGrant.Purchase;

    /// <summary>
    /// Which way they are attending. Only meaningful when the experience allows both; null means
    /// "however the experience runs".
    /// </summary>
    public BookingAttendance? Attendance { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
}
