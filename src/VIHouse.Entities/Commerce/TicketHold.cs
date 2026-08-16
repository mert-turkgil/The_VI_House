using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

/// <summary>
/// The no-oversell reservation mechanism (brief §24/§179). Created only after
/// ICapacityService.TryReserveAsync succeeds in atomically decrementing TicketType.Inventory;
/// released back to inventory on expiry/cancellation, or Committed once a webhook confirms
/// payment.
/// </summary>
public class TicketHold : BaseEntity
{
    public Guid TicketTypeId { get; set; }
    public Guid? ApplicationId { get; set; }
    public Guid? InvitationId { get; set; }
    public int Quantity { get; set; }
    public TicketHoldStatus Status { get; set; } = TicketHoldStatus.Active;
    public DateTimeOffset ExpiresAt { get; set; }
}
