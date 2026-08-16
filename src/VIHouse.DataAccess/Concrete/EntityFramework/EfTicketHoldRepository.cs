using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfTicketHoldRepository(VIHouseDbContext db) : EfRepository<TicketHold>(db), ITicketHoldRepository
{
    public Task<List<TicketHold>> GetExpiredActiveHoldsAsync(DateTimeOffset cutoff, CancellationToken ct = default) =>
        Set.Where(h => h.Status == TicketHoldStatus.Active && h.ExpiresAt <= cutoff).ToListAsync(ct);
}
