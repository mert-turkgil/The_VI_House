using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfUserDirectory(VIHouseDbContext db) : IUserDirectory
{
    public async Task<UserDirectoryPage> SearchAsync(string? query, string? role, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var users = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            users = users.Where(u =>
                (u.Email != null && u.Email.Contains(q)) ||
                u.FirstName.Contains(q) || u.LastName.Contains(q) ||
                (u.FirstName + " " + u.LastName).Contains(q) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (role == "none")
                users = users.Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id));
            else
            {
                var roleId = await db.Roles.Where(r => r.Name == role).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
                users = roleId is { } id ? users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == id)) : users.Where(_ => false);
            }
        }

        var total = await users.CountAsync(ct);
        var slice = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new UserDirectoryPage(await ToRowsAsync(slice, ct), total, page, pageSize);
    }

    public async Task<UserDirectoryStats> GetStatsAsync(DateTimeOffset since, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var users = db.Users.AsNoTracking();

        var founderRoleId = await db.Roles.Where(r => r.Name == Roles.Founder).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        var memberRoleId = await db.Roles.Where(r => r.Name == Roles.Member).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);

        var newest = await users.Where(u => u.CreatedAt >= since).OrderByDescending(u => u.CreatedAt).Take(6).ToListAsync(ct);

        return new UserDirectoryStats(
            TotalUsers: await users.CountAsync(ct),
            NewSince: await users.CountAsync(u => u.CreatedAt >= since, ct),
            UnconfirmedEmail: await users.CountAsync(u => !u.EmailConfirmed, ct),
            LockedOut: await users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now, ct),
            Founders: founderRoleId is { } f ? await db.UserRoles.CountAsync(ur => ur.RoleId == f, ct) : 0,
            Members: memberRoleId is { } m ? await db.UserRoles.CountAsync(ur => ur.RoleId == m, ct) : 0,
            Newest: await ToRowsAsync(newest, ct));
    }

    private async Task<List<UserDirectoryRow>> ToRowsAsync(List<ApplicationUser> slice, CancellationToken ct)
    {
        if (slice.Count == 0) return [];
        var ids = slice.Select(u => u.Id).ToList();
        var now = DateTimeOffset.UtcNow;

        var roles = await (from ur in db.UserRoles
                           join r in db.Roles on ur.RoleId equals r.Id
                           where ids.Contains(ur.UserId)
                           select new { ur.UserId, r.Name }).ToListAsync(ct);
        var rolesByUser = roles.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Name!).OrderBy(n => n).ToList());

        var titles = await db.Profiles.AsNoTracking().Where(p => ids.Contains(p.UserId))
            .Select(p => new { p.UserId, p.JobTitle }).ToListAsync(ct);
        var titleByUser = titles.GroupBy(t => t.UserId).ToDictionary(g => g.Key, g => g.First().JobTitle);

        var appCounts = await db.Applications.Where(a => a.UserId != null && ids.Contains(a.UserId.Value))
            .GroupBy(a => a.UserId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var bookingCounts = await db.Bookings.Where(b => ids.Contains(b.UserId))
            .GroupBy(b => b.UserId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return slice.Select(u => new UserDirectoryRow(
            u.Id, u.Email ?? u.UserName ?? "—", $"{u.FirstName} {u.LastName}".Trim(), u.CreatedAt, u.LastLoginAt,
            u.EmailConfirmed, u.LockoutEnd is { } end && end > now,
            rolesByUser.GetValueOrDefault(u.Id) ?? [], titleByUser.GetValueOrDefault(u.Id),
            appCounts.GetValueOrDefault(u.Id), bookingCounts.GetValueOrDefault(u.Id))).ToList();
    }
}
