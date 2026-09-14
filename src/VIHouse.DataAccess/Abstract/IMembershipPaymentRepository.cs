using VIHouse.Entities.Commerce;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Abstract;

public interface IMembershipPaymentRepository : IRepository<MembershipPayment>
{
    Task<MembershipPayment?> GetByProviderReferenceAsync(string providerReference, CancellationToken ct = default);

    /// <summary>Atomically moves the row from <paramref name="from"/> to <paramref name="to"/> and
    /// says whether this call did it — so two deliveries of the same completion webhook cannot
    /// both provision. Runs outside the change tracker; see IPendingJoinRepository.TryClaimAsync.</summary>
    Task<bool> TryClaimAsync(Guid id, PaymentStatus from, PaymentStatus to, CancellationToken ct = default);
}
