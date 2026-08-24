using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfSeminarEnrollmentRepository(VIHouseDbContext db)
    : EfRepository<SeminarEnrollment>(db), ISeminarEnrollmentRepository
{
    public Task<SeminarEnrollment?> GetForUserAsync(Guid seminarId, Guid userId, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(e => e.SeminarId == seminarId && e.UserId == userId, ct);

    public Task<SeminarEnrollment?> GetByProviderReferenceAsync(string providerReference, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(e => e.ProviderReference == providerReference, ct);

    public async Task<List<SeminarEnrollment>> GetForSeminarAsync(Guid seminarId, CancellationToken ct = default) =>
        await Set.Where(e => e.SeminarId == seminarId)
                 .OrderByDescending(e => e.CreatedAt)
                 .ToListAsync(ct);

    public async Task<List<SeminarEnrollment>> GetConfirmedForUserAsync(Guid userId, CancellationToken ct = default) =>
        await Set.Where(e => e.UserId == userId && e.Status == SeminarEnrollmentStatus.Confirmed)
                 .OrderByDescending(e => e.ConfirmedAt)
                 .ToListAsync(ct);

    public Task<int> CountTakenAsync(Guid seminarId, CancellationToken ct = default) =>
        Set.CountAsync(e => e.SeminarId == seminarId && e.Status != SeminarEnrollmentStatus.Cancelled, ct);
}
