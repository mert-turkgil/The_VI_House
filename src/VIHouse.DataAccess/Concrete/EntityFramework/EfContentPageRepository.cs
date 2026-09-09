using Microsoft.EntityFrameworkCore;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Concrete.EntityFramework;

public class EfContentPageRepository(VIHouseDbContext db) : EfRepository<ContentPage>(db), IContentPageRepository
{
    public Task<ContentPage?> GetBySlugWithBlocksAsync(string slug, CancellationToken ct = default) =>
        Set.Include(p => p.Blocks.OrderBy(b => b.SortOrder))
                // Without this the resolver sees an empty translation list on every block and
                // silently serves English to all four languages — which is exactly what the
                // homepage did before the table existed, so the failure would look like no change
                // at all rather than like a bug.
                .ThenInclude(b => b.Translations)
            .FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished, ct);
}
