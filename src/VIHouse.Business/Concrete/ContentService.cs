using System.Text.Json;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Content;

namespace VIHouse.Business.Concrete;

public class ContentService(
    IContentPageRepository pages,
    IRepository<ContentBlock> blocks,
    IAuditLogRepository auditLogs) : IContentService
{
    public Task<List<ContentPage>> GetAllPagesAsync(CancellationToken ct = default) => pages.GetAllAsync(ct);

    public Task<ContentPage?> GetPageWithBlocksAsync(string slug, CancellationToken ct = default) =>
        pages.GetBySlugWithBlocksAsync(slug, ct);

    public Task<ContentBlock?> GetBlockAsync(Guid id, CancellationToken ct = default) => blocks.GetByIdAsync(id, ct);

    public async Task<ContentBlock> AddBlockAsync(Guid pageId, ContentBlock block, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        block.PageId = pageId;
        await blocks.AddAsync(block, ct);
        await LogAsync("ContentBlockAdded", block.Id, adminUserId, ipAddress,
            before: null, after: new { block.SectionKey, block.Heading, PageId = pageId }, ct);
        await blocks.SaveChangesAsync(ct);
        return block;
    }

    public async Task UpdateBlockAsync(ContentBlock updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await blocks.GetByIdAsync(updated.Id, ct)
            ?? throw new InvalidOperationException($"Content block {updated.Id} not found.");

        var before = new { existing.Heading, existing.Subheading, existing.BodyText, existing.CtaLabel, existing.CtaUrl };

        existing.SectionKey = updated.SectionKey;
        existing.SortOrder = updated.SortOrder;
        existing.Heading = updated.Heading;
        existing.Subheading = updated.Subheading;
        existing.BodyText = updated.BodyText;
        existing.ImageUrl = updated.ImageUrl;
        existing.CtaLabel = updated.CtaLabel;
        existing.CtaUrl = updated.CtaUrl;
        existing.ExtraJson = updated.ExtraJson;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("ContentBlockUpdated", existing.Id, adminUserId, ipAddress,
            before, new { existing.Heading, existing.Subheading, existing.BodyText, existing.CtaLabel, existing.CtaUrl }, ct);

        // No explicit Update() call: `existing` was loaded via GetByIdAsync on this same scoped
        // DbContext, so it's already tracked — same reasoning as ExperienceService.UpdateCoreFieldsAsync.
        await blocks.SaveChangesAsync(ct);
    }

    public async Task RemoveBlockAsync(Guid pageId, Guid blockId, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var block = await blocks.GetByIdAsync(blockId, ct);
        if (block is null || block.PageId != pageId) return;

        blocks.Remove(block);
        await LogAsync("ContentBlockRemoved", blockId, adminUserId, ipAddress,
            before: new { block.SectionKey, PageId = pageId }, after: null, ct);
        await blocks.SaveChangesAsync(ct);
    }

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(ContentBlock),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
