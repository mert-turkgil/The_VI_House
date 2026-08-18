using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Users;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfProfileRepository(VIHouseDbContext db) : IProfileRepository
{
    public Task<Profile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        db.Profiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public Task AddAsync(Profile profile, CancellationToken ct = default) =>
        db.Profiles.AddAsync(profile, ct).AsTask();

    public void Update(Profile profile) => db.Profiles.Update(profile);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<List<MemberDirectoryEntry>> SearchDirectoryAsync(ProfileFilter filter, CancellationToken ct = default)
    {
        // Profile has no C# navigation property to ApplicationUser (Entities must stay
        // Identity-agnostic — see Profile.cs), but both DbSets live on this same DbContext and the
        // FK is configured at the model level (ProfileConfiguration.cs), so a plain join here is a
        // single efficient query rather than the N+1 UserManager lookups AdminUsersController uses.
        var query =
            from profile in db.Profiles
            join user in db.Users on profile.UserId equals user.Id
            where profile.Visibility == ProfileVisibility.MembersOnly
            select new { profile, user };

        if (!string.IsNullOrWhiteSpace(filter.Industry))
            query = query.Where(x => x.profile.Industry != null && x.profile.Industry.Contains(filter.Industry));
        if (!string.IsNullOrWhiteSpace(filter.City))
            query = query.Where(x => x.user.City == filter.City);
        if (!string.IsNullOrWhiteSpace(filter.Country))
            query = query.Where(x => x.user.Country == filter.Country);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search;
            query = query.Where(x =>
                x.user.FirstName.Contains(s) || x.user.LastName.Contains(s) ||
                (x.profile.CompanyName != null && x.profile.CompanyName.Contains(s)) ||
                (x.profile.JobTitle != null && x.profile.JobTitle.Contains(s)) ||
                (x.profile.Interests != null && x.profile.Interests.Contains(s)) ||
                (x.profile.CanHelpWith != null && x.profile.CanHelpWith.Contains(s)));
        }

        return await query
            .OrderBy(x => x.user.FirstName).ThenBy(x => x.user.LastName)
            .Skip(filter.Skip).Take(filter.Take)
            .Select(x => new MemberDirectoryEntry(
                x.user.Id, x.user.FirstName, x.user.LastName, x.user.City, x.user.Country,
                x.profile.CompanyName, x.profile.JobTitle, x.profile.Industry, x.profile.Bio,
                x.profile.PhotoUrl, x.profile.LinkedInUrl, x.profile.Interests, x.profile.LookingFor, x.profile.CanHelpWith))
            .ToListAsync(ct);
    }

    public async Task<MemberDirectoryEntry?> GetDirectoryEntryAsync(Guid userId, CancellationToken ct = default)
    {
        var result =
            from profile in db.Profiles
            join user in db.Users on profile.UserId equals user.Id
            where profile.Visibility == ProfileVisibility.MembersOnly && user.Id == userId
            select new MemberDirectoryEntry(
                user.Id, user.FirstName, user.LastName, user.City, user.Country,
                profile.CompanyName, profile.JobTitle, profile.Industry, profile.Bio,
                profile.PhotoUrl, profile.LinkedInUrl, profile.Interests, profile.LookingFor, profile.CanHelpWith);

        return await result.FirstOrDefaultAsync(ct);
    }
}
