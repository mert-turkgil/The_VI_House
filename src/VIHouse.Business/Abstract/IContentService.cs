using VIHouse.Entities.Content;

namespace VIHouse.Business.Abstract;

/// <summary>Admin-facing CMS operations — the public read side (HomeController) goes straight to IContentPageRepository since it has no auditing/mutation concerns.</summary>
public interface IContentService
{
    Task<List<ContentPage>> GetAllPagesAsync(CancellationToken ct = default);
    Task<ContentPage?> GetPageWithBlocksAsync(string slug, CancellationToken ct = default);
    Task<ContentBlock?> GetBlockAsync(Guid id, CancellationToken ct = default);

    Task<ContentBlock> AddBlockAsync(Guid pageId, ContentBlock block, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task UpdateBlockAsync(ContentBlock updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveBlockAsync(Guid pageId, Guid blockId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}
