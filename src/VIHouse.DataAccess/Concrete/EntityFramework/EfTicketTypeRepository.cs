using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfTicketTypeRepository(VIHouseDbContext db) : EfRepository<TicketType>(db), ITicketTypeRepository
{
    public Task<List<TicketType>> GetByExperienceAsync(Guid experienceId, CancellationToken ct = default) =>
        Set.Where(t => t.ExperienceId == experienceId).OrderBy(t => t.SortOrder).ToListAsync(ct);

    public async Task<bool> TryDecrementInventoryAsync(Guid ticketTypeId, int quantity, CancellationToken ct = default)
    {
        // Single round-trip conditional UPDATE ... WHERE Inventory >= @quantity. Zero rows affected
        // means sold-out or a lost race — the caller (CapacityService) must never fall back to a
        // separate read-then-write, or the whole point of doing this atomically is lost.
        var affected = await Set
            .Where(t => t.Id == ticketTypeId && t.Inventory >= quantity)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Inventory, t => t.Inventory - quantity), ct);

        return affected > 0;
    }

    public async Task IncrementInventoryAsync(Guid ticketTypeId, int quantity, CancellationToken ct = default)
    {
        await Set
            .Where(t => t.Id == ticketTypeId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.Inventory, t => t.Inventory + quantity), ct);
    }
}
