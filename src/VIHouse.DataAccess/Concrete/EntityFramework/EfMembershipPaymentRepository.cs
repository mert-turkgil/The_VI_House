using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfMembershipPaymentRepository(VIHouseDbContext db) : EfRepository<MembershipPayment>(db), IMembershipPaymentRepository
{
    public Task<MembershipPayment?> GetByProviderReferenceAsync(string providerReference, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.ProviderReference == providerReference, ct);
}
