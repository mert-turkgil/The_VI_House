using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfContentPageRepository(VIHouseDbContext db) : EfRepository<ContentPage>(db), IContentPageRepository
{
    public Task<ContentPage?> GetBySlugWithBlocksAsync(string slug, CancellationToken ct = default) =>
        Set.Include(p => p.Blocks.OrderBy(b => b.SortOrder))
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
}
