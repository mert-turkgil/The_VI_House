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

    public Task<List<Application>> GetForApplicantAsync(IReadOnlyCollection<Guid> ids, Guid? userId, string? email, CancellationToken ct = default)
    {
        var address = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        if (ids.Count == 0 && userId is null && address is null) return Task.FromResult(new List<Application>());

        return Set.AsNoTracking()
            .Where(a => ids.Contains(a.Id)
                        || (userId != null && a.UserId == userId)
                        || (address != null && EF.Functions.Collate(a.Email, "Latin1_General_CI_AS") == address))
            .OrderByDescending(a => a.SubmittedAt ?? a.CreatedAt)
            .ToListAsync(ct);
    }
}
