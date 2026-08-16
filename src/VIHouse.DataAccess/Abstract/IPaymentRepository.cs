using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Abstract;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByProviderReferenceAsync(string providerReference, CancellationToken ct = default);
    Task<List<Payment>> GetByApplicationAsync(Guid applicationId, CancellationToken ct = default);
}
