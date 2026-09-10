using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Marketing;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfNotifySignupRepository(VIHouseDbContext db) : EfRepository<NotifySignup>(db), INotifySignupRepository
{
    public Task<NotifySignup?> FindByEmailAsync(string email, CancellationToken ct = default)
    {
        // Lowercased in C# on both sides rather than left to the database, exactly as
        // EfWaitlistRepository.FindByEmailAsync does and for the same reason: this column is
        // compared under the database's Turkish collation, where "I" and "ı" are separate letters
        // and the uppercase of "i" is "İ". An address typed with a capital I would not match the
        // row stored from the same address in lower case, and the unique index would happily accept
        // both. Normalising before the comparison removes the case-folding question entirely, which
        // is why EF.Functions.Collate is not used here — it would paper over the problem rather than
        // settle it.
        //
        // ToLowerInvariant, never ToLower: the process culture is en-GB today, but a background
        // caller's need not be, and under tr-TR "I".ToLower() is "ı".
        var normalised = email.Trim().ToLowerInvariant();
        return Set.FirstOrDefaultAsync(n => n.Email == normalised, ct);
    }

    public async Task<List<NotifySignup>> GetRecentAsync(int skip, int take, CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountAsync(CancellationToken ct = default) => Set.CountAsync(ct);
}
