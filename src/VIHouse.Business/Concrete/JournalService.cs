using System.Text.Json;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Journal;

namespace VIHouse.Business.Concrete;

public class JournalService(IJournalPostRepository posts, IAuditLogRepository auditLogs) : IJournalService
{
    public Task<List<JournalPost>> GetPublicListingAsync(JournalPostFilter filter, CancellationToken ct = default) =>
        posts.GetPublicListingAsync(filter, ct);

    public Task<JournalPost?> GetPublicDetailBySlugAsync(string slug, CancellationToken ct = default) =>
        posts.GetBySlugAsync(slug, ct);

    public async Task<List<JournalPost>> GetAllForAdminAsync(CancellationToken ct = default) =>
        (await posts.GetAllAsync(ct)).OrderByDescending(p => p.CreatedAt).ToList();

    public Task<JournalPost?> GetForAdminEditAsync(Guid id, CancellationToken ct = default) =>
        posts.GetByIdAsync(id, ct);

    public async Task<JournalPost> CreateAsync(JournalPost post, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        if (post.Status == JournalPostStatus.Published)
            post.PublishedAt = DateTimeOffset.UtcNow;

        await posts.AddAsync(post, ct);
        await LogAsync("JournalPostCreated", post.Id, adminUserId, ipAddress,
            before: null, after: new { post.Title, post.Slug, post.Category, post.Status }, ct);
        await posts.SaveChangesAsync(ct);
        return post;
    }

    public async Task UpdateAsync(JournalPost updated, Guid adminUserId, string? ipAddress, CancellationToken ct = default)
    {
        var existing = await posts.GetByIdAsync(updated.Id, ct)
            ?? throw new InvalidOperationException($"Journal post {updated.Id} not found.");

        var before = new { existing.Title, existing.Slug, existing.Category, existing.Status };

        existing.Title = updated.Title;
        existing.Slug = updated.Slug;
        existing.Category = updated.Category;
        existing.Excerpt = updated.Excerpt;
        existing.Body = updated.Body;
        existing.CoverImageUrl = updated.CoverImageUrl;
        existing.AuthorName = updated.AuthorName;
        existing.Status = updated.Status;
        if (existing.PublishedAt is null && updated.Status == JournalPostStatus.Published)
            existing.PublishedAt = DateTimeOffset.UtcNow;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await LogAsync("JournalPostUpdated", existing.Id, adminUserId, ipAddress,
            before, new { existing.Title, existing.Slug, existing.Category, existing.Status }, ct);

        // No explicit Update() call: `existing` is already tracked, loaded on this same scoped
        // DbContext — same reasoning as ExperienceService.UpdateCoreFieldsAsync.
        await posts.SaveChangesAsync(ct);
    }

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(JournalPost),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
