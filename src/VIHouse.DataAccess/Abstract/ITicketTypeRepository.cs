using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Abstract;

public interface ITicketTypeRepository : IRepository<TicketType>
{
    Task<List<TicketType>> GetByExperienceAsync(Guid experienceId, CancellationToken ct = default);

    /// <summary>
    /// Atomic, conditional inventory decrement: affects 0 rows (and returns false) if insufficient
    /// stock remains, without any app-level check-then-act race window. Used exclusively by
    /// VIHouse.Business's CapacityService — never call Update(TicketType) to change Inventory.
    /// </summary>
    Task<bool> TryDecrementInventoryAsync(Guid ticketTypeId, int quantity, CancellationToken ct = default);

    Task IncrementInventoryAsync(Guid ticketTypeId, int quantity, CancellationToken ct = default);
}
