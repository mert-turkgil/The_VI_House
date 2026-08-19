using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IBookingRepository : IRepository<Booking>
{
    Task<Booking?> GetByReferenceAsync(string bookingReference, CancellationToken ct = default);
    Task<List<Booking>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<List<Booking>> GetByExperienceAsync(Guid experienceId, CancellationToken ct = default);

    /// <summary>
    /// Atomically allocates the next human-readable reference for a given year (e.g. "VI-26-1042")
    /// via a SQL sequence object, so two simultaneous webhook deliveries can never collide on the
    /// same reference (brief §110).
    /// </summary>
    Task<string> GenerateNextReferenceAsync(int twoDigitYear, CancellationToken ct = default);
}
