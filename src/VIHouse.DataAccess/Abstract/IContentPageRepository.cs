using VIHouse.Entities.Content;

namespace VIHouse.DataAccess.Abstract;

public interface IContentPageRepository : IRepository<ContentPage>
{
    Task<ContentPage?> GetBySlugWithBlocksAsync(string slug, CancellationToken ct = default);
}
