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

    /// <summary>
    /// Writes one language's copy of one homepage section. Returns a resource key on failure.
    ///
    /// The English original stays on the block itself and is edited through UpdateBlockAsync; this
    /// only ever touches the other three. Passing the default culture is refused rather than
    /// quietly writing a row that would shadow the column every other language falls back to.
    /// </summary>
    Task<string?> SaveBlockTranslationAsync(
        ContentBlockTranslation form, Guid adminUserId, string? ipAddress, CancellationToken ct = default);

    /// <summary>Removes one language's copy, so a section can fall back to English again.</summary>
    Task<string?> DeleteBlockTranslationAsync(
        Guid blockId, string culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
    Task RemoveBlockAsync(Guid pageId, Guid blockId, Guid adminUserId, string? ipAddress, CancellationToken ct = default);
}
