using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfMembershipPaymentRepository(VIHouseDbContext db) : EfRepository<MembershipPayment>(db), IMembershipPaymentRepository
{
    public Task<MembershipPayment?> GetByProviderReferenceAsync(string providerReference, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.ProviderReference == providerReference, ct);

    public async Task<bool> TryClaimAsync(Guid id, PaymentStatus from, PaymentStatus to, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var affected = await Set
            .Where(p => p.Id == id && p.Status == from)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(p => p.Status, to)
                .SetProperty(p => p.UpdatedAt, now), ct);

        return affected == 1;
    }
}
