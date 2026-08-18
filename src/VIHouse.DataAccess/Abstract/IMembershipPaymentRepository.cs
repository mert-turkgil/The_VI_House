using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Abstract;

public interface IMembershipPaymentRepository : IRepository<MembershipPayment>
{
    Task<MembershipPayment?> GetByProviderReferenceAsync(string providerReference, CancellationToken ct = default);
}
