using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Applications;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfApplicationRepository(VIHouseDbContext db) : EfRepository<Application>(db), IApplicationRepository
{
    public Task<List<Application>> GetByStatusAsync(ApplicationStatus status, CancellationToken ct = default) =>
        Set.Where(a => a.Status == status).OrderByDescending(a => a.SubmittedAt).ToListAsync(ct);

    public Task<List<Application>> GetByExperienceAsync(Guid experienceId, CancellationToken ct = default) =>
        Set.Where(a => a.ExperienceId == experienceId).OrderByDescending(a => a.SubmittedAt).ToListAsync(ct);

    public Task<Application?> GetWithTagsAsync(Guid id, CancellationToken ct = default) =>
        Set.Include(a => a.Tags).FirstOrDefaultAsync(a => a.Id == id, ct);
}
