using VIHouse.Entities.Commerce;

namespace VIHouse.Business.Abstract;

/// <summary>The no-oversell reservation mechanism (brief §24/§177-179) — kept separate from payment/booking logic.</summary>
public interface ICapacityService
{
    /// <summary>Atomically decrements inventory and creates an Active hold for as long as the
    /// provider keeps the checkout open (30 minutes — the two are aligned on purpose, so a seat
    /// cannot go back on sale while its checkout can still be paid). Null means sold out / lost the race.</summary>
    Task<TicketHold?> TryReserveAsync(Guid ticketTypeId, int quantity, Guid applicationId, Guid? invitationId, CancellationToken ct = default);

    /// <summary>The checkout completed but the money is still clearing (a delayed payment method):
    /// keep the seat until then. No-op unless the hold is Active.</summary>
    Task ExtendAsync(Guid holdId, DateTimeOffset until, CancellationToken ct = default);

    /// <summary>Payment confirmed — the hold's inventory decrement becomes permanent.</summary>
    Task CommitAsync(Guid holdId, CancellationToken ct = default);

    /// <summary>Checkout abandoned/failed before payment — gives the seat back immediately rather than waiting for the sweep.</summary>
    Task ReleaseAsync(Guid holdId, CancellationToken ct = default);

    /// <summary>Background sweep (brief §177-179): returns inventory for holds nobody ever completed checkout on.</summary>
    Task<int> ReleaseExpiredHoldsAsync(CancellationToken ct = default);
}
