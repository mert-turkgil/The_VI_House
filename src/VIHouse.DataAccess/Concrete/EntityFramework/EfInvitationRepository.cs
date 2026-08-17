using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfInvitationRepository(VIHouseDbContext db) : EfRepository<Invitation>(db), IInvitationRepository
{
    public Task<Invitation?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(i => i.Code == code, ct);

    public Task<Invitation?> GetLatestByApplicationAsync(Guid applicationId, CancellationToken ct = default) =>
        Set.Where(i => i.ApplicationId == applicationId).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(ct);
}
