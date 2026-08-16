using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfBookingRepository(VIHouseDbContext db) : EfRepository<Booking>(db), IBookingRepository
{
    public Task<Booking?> GetByReferenceAsync(string bookingReference, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(b => b.BookingReference == bookingReference, ct);

    public Task<List<Booking>> GetByUserAsync(Guid userId, CancellationToken ct = default) =>
        Set.Where(b => b.UserId == userId).OrderByDescending(b => b.CreatedAt).ToListAsync(ct);

    public async Task<string> GenerateNextReferenceAsync(int twoDigitYear, CancellationToken ct = default)
    {
        var next = await Db.Database
            .SqlQuery<long>($"SELECT NEXT VALUE FOR dbo.BookingRefSeq")
            .SingleAsync(ct);

        return $"VI-{twoDigitYear:D2}-{next}";
    }
}
