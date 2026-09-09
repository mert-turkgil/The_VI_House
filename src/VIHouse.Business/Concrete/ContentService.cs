using System.Text.Json;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Content;

namespace VIHouse.Business.Concrete;

public class ContentService(
    IContentPageRepository pages,
    IRepository<ContentBlock> blocks,
    IRepository<ContentBlockTranslation> blockTranslations,
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

    // --- Translations ---------------------------------------------------------------------------
    // The homepage below the hero used to read English in every language, because ContentBlock held
    // one row per section and there was nowhere to put the other three. These two methods are that
    // "somewhere". The English original is never one of these rows — it stays on the block.

    public async Task<string?> SaveBlockTranslationAsync(
        ContentBlockTranslation form, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (!SiteCultures.IsSupported(form.Culture))
            return "Admin.Cms.UnknownCulture";

        var culture = SiteCultures.Normalise(form.Culture);

        // The English copy lives on the block's own columns and is what every other language falls
        // back to field by field. A row here for the default culture would shadow it invisibly:
        // the section would keep rendering, just from a second place nobody thinks to look in.
        if (culture == SiteCultures.Default)
            return "Admin.Cms.DefaultIsOnTheBlock";

        var block = await blocks.GetByIdAsync(form.ContentBlockId, ct);
        if (block is null) return "Admin.Cms.BlockNotFound";

        // Invalid JSON is refused rather than stored, matching what UpdateSection already does for
        // the English payload: saving it would empty the section for that language alone, with no
        // error anywhere and nothing on the English page to hint at it.
        var extraJson = string.IsNullOrWhiteSpace(form.ExtraJson) ? null : form.ExtraJson.Trim();
        if (extraJson is not null && !IsValidJson(extraJson))
            return "Admin.Cms.InvalidJson";

        var existing = await FindTranslationAsync(form.ContentBlockId, culture, ct);

        if (existing is null)
        {
            existing = new ContentBlockTranslation { ContentBlockId = form.ContentBlockId, Culture = culture };
            await blockTranslations.AddAsync(existing, ct);
        }

        existing.Heading = Trim(form.Heading);
        existing.Subheading = Trim(form.Subheading);
        existing.BodyText = Trim(form.BodyText);
        existing.CtaLabel = Trim(form.CtaLabel);
        existing.ExtraJson = extraJson;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("ContentBlockTranslationSaved", form.ContentBlockId, adminUserId, ipAddress,
            before: null, after: new { culture, block.SectionKey }, ct);

        await blocks.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> DeleteBlockTranslationAsync(
        Guid blockId, string culture, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var normalised = SiteCultures.Normalise(culture);

        if (normalised == SiteCultures.Default)
            return "Admin.Cms.DefaultIsOnTheBlock";

        var existing = await FindTranslationAsync(blockId, normalised, ct);
        if (existing is null) return null;

        blockTranslations.Remove(existing);

        await LogAsync("ContentBlockTranslationRemoved", blockId, adminUserId, ipAddress,
            before: new { normalised, existing.Heading }, after: null, ct);

        await blocks.SaveChangesAsync(ct);
        return null;
    }

    private async Task<ContentBlockTranslation?> FindTranslationAsync(Guid blockId, string culture, CancellationToken ct)
    {
        var all = await blockTranslations.GetAllAsync(ct);
        return all.FirstOrDefault(t =>
            t.ContentBlockId == blockId
            && string.Equals(t.Culture, culture, StringComparison.OrdinalIgnoreCase));
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
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
