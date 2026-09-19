using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.Business.Concrete;

public class CapacityService(ITicketTypeRepository ticketTypes, ITicketHoldRepository holds) : ICapacityService
{
    /// <summary>Matches the provider's checkout window (StripePaymentProvider sets ExpiresAt to 30
    /// minutes). A shorter hold used to hand the seat back while the buyer could still pay for it.</summary>
    public static readonly TimeSpan HoldDuration = TimeSpan.FromMinutes(30);

    public async Task<TicketHold?> TryReserveAsync(Guid ticketTypeId, int quantity, Guid applicationId, Guid? invitationId, CancellationToken ct = default)
    {
        // The atomic conditional UPDATE is the entire no-oversell guarantee — zero affected rows
        // means sold out or a lost race, full stop. Nothing here may fall back to reading Inventory
        // and deciding client-side.
        var reserved = await ticketTypes.TryDecrementInventoryAsync(ticketTypeId, quantity, ct);
        if (!reserved) return null;

        var hold = new TicketHold
        {
            TicketTypeId = ticketTypeId,
            ApplicationId = applicationId,
            InvitationId = invitationId,
            Quantity = quantity,
            Status = TicketHoldStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.Add(HoldDuration),
        };
        await holds.AddAsync(hold, ct);
        await holds.SaveChangesAsync(ct);
        return hold;
    }

    public async Task ExtendAsync(Guid holdId, DateTimeOffset until, CancellationToken ct = default)
    {
        var hold = await holds.GetByIdAsync(holdId, ct);
        if (hold is null || hold.Status != TicketHoldStatus.Active || hold.ExpiresAt >= until) return;

        hold.ExpiresAt = until;
        hold.UpdatedAt = DateTimeOffset.UtcNow;
        await holds.SaveChangesAsync(ct);
    }

    public async Task CommitAsync(Guid holdId, CancellationToken ct = default)
    {
        var hold = await holds.GetByIdAsync(holdId, ct)
            ?? throw new InvalidOperationException($"TicketHold {holdId} not found.");

        if (hold.Status != TicketHoldStatus.Active)
            return; // already committed/released — webhook redelivery, no-op

        hold.Status = TicketHoldStatus.Committed;
        hold.UpdatedAt = DateTimeOffset.UtcNow;
        await holds.SaveChangesAsync(ct);
    }

    public async Task ReleaseAsync(Guid holdId, CancellationToken ct = default)
    {
        var hold = await holds.GetByIdAsync(holdId, ct);
        if (hold is null || hold.Status != TicketHoldStatus.Active) return;

        hold.Status = TicketHoldStatus.Released;
        hold.UpdatedAt = DateTimeOffset.UtcNow;
        await ticketTypes.IncrementInventoryAsync(hold.TicketTypeId, hold.Quantity, ct);
        await holds.SaveChangesAsync(ct);
    }

    public async Task<int> ReleaseExpiredHoldsAsync(CancellationToken ct = default)
    {
        var expired = await holds.GetExpiredActiveHoldsAsync(DateTimeOffset.UtcNow, ct);
        foreach (var hold in expired)
        {
            hold.Status = TicketHoldStatus.Expired;
            hold.UpdatedAt = DateTimeOffset.UtcNow;
            await ticketTypes.IncrementInventoryAsync(hold.TicketTypeId, hold.Quantity, ct);
        }

        if (expired.Count > 0)
            await holds.SaveChangesAsync(ct);

        return expired.Count;
    }
}
