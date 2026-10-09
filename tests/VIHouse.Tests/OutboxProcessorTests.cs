using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.DataAccess.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Notifications;

namespace VIHouse.Tests;

public class OutboxProcessorTests
{
    [Fact]
    public async Task Email_the_server_refused_is_retried_not_marked_delivered()
    {
        var repo = new InMemoryOutboxRepository();
        var outbox = new Outbox(repo, NullLogger<Outbox>.Instance);
        await outbox.EnqueueEmailAsync("k1", "TestEmail", "a@example.com", "Hi",
            new TestEmailModel("admin", DateTimeOffset.UtcNow, "smtp"), "en-GB");

        var email = new ScriptedEmailService(false, true);
        var processor = new OutboxProcessor(repo, email, new NoSms(), new NoNotifications(), NullLogger<OutboxProcessor>.Instance);

        await processor.ProcessDueAsync(10);
        var message = repo.Rows.Single();
        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.Attempts);
        Assert.Contains("not sent", message.LastError);
        Assert.True(message.NextAttemptAt > DateTimeOffset.UtcNow);

        // Due again (as if the backoff had passed) — the server now accepts it.
        message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await processor.ProcessDueAsync(10);
        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.LastError);
        Assert.Equal(2, email.Calls);
    }

    private sealed class ScriptedEmailService(params bool[] results) : IEmailService
    {
        public int Calls { get; private set; }

        public Task<bool> SendAsync<TModel>(string templateKey, string recipientEmail, string subject, TModel model, string culture,
            string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default) =>
            Task.FromResult(results[Math.Min(Calls++, results.Length - 1)]);

        public Task<ResendResult> ResendAsync(Guid logId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoSms : ISmsService
    {
        public bool IsConfigured => false;
        public Task<bool> SendAsync(string templateKey, string? recipientPhone, string body, string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task<ResendResult> ResendAsync(Guid logId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class NoNotifications : INotificationService
    {
        public Task CreateForUserAsync(Guid userId, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateForEmailAsync(string email, NotificationType type, string title, string body, string? link = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<Notification>> GetForUserAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(new List<Notification>());
        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(0);
        public Task MarkReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task MarkAllReadAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> BroadcastToExperienceAttendeesAsync(Guid experienceId, NotificationType type, string title, string body, string? link, Guid adminUserId, string? ipAddress, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class InMemoryOutboxRepository : IOutboxRepository
    {
        public List<OutboxMessage> Rows { get; } = [];

        public Task<bool> ExistsAsync(string dedupeKey, CancellationToken ct = default) => Task.FromResult(Rows.Any(r => r.DedupeKey == dedupeKey));

        public Task<List<OutboxMessage>> GetDueAsync(DateTimeOffset now, int take, int maxAttempts, CancellationToken ct = default) =>
            Task.FromResult(Rows.Where(m => m.ProcessedAt == null && m.NextAttemptAt <= now && m.Attempts < maxAttempts)
                .OrderBy(m => m.NextAttemptAt).Take(take).ToList());

        public Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Rows.FirstOrDefault(r => r.Id == id));
        public Task<List<OutboxMessage>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Rows.ToList());
        public Task<List<OutboxMessage>> FindAsync(Expression<Func<OutboxMessage, bool>> predicate, CancellationToken ct = default) => Task.FromResult(Rows.AsQueryable().Where(predicate).ToList());
        public Task<int> CountAsync(Expression<Func<OutboxMessage, bool>> predicate, CancellationToken ct = default) => Task.FromResult(Rows.AsQueryable().Count(predicate));
        public Task AddAsync(OutboxMessage entity, CancellationToken ct = default) { Rows.Add(entity); return Task.CompletedTask; }
        public void Update(OutboxMessage entity) { }
        public void Remove(OutboxMessage entity) => Rows.Remove(entity);
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}
