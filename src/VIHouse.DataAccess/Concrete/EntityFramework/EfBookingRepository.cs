using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        // Deliberately not Database.SqlQuery<T>() — EF Core always wraps that in a derived-table
        // subquery ("SELECT ... FROM (<sql>) AS [s]"), and SQL Server rejects NEXT VALUE FOR inside
        // any subquery/view/function. Running it as a standalone top-level statement over the same
        // connection (and ambient transaction, if any) is the only way to call a sequence via EF.
        var connection = Db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT NEXT VALUE FOR dbo.BookingRefSeq";
        if (Db.Database.CurrentTransaction is not null)
            command.Transaction = Db.Database.CurrentTransaction.GetDbTransaction();

        var next = (long)(await command.ExecuteScalarAsync(ct))!;
        return $"VI-{twoDigitYear:D2}-{next}";
    }
}
