using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface ITicketHoldRepository : IRepository<TicketHold>
{
    /// <summary>Feeds the 60s background sweep that releases abandoned holds (brief §177-179).</summary>
    Task<List<TicketHold>> GetExpiredActiveHoldsAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    /// <summary>The in-flight hold for a checkout attempt — at most one Active hold per Application at a time.</summary>
    Task<TicketHold?> GetActiveByApplicationAsync(Guid applicationId, CancellationToken ct = default);
}
