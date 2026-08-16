using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfPaymentRepository(VIHouseDbContext db) : EfRepository<Payment>(db), IPaymentRepository
{
    public Task<Payment?> GetByProviderReferenceAsync(string providerReference, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.ProviderReference == providerReference, ct);

    public Task<List<Payment>> GetByApplicationAsync(Guid applicationId, CancellationToken ct = default) =>
        Set.Where(p => p.ApplicationId == applicationId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
}
