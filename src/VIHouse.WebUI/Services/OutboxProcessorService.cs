using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.Services;

/// <summary>
/// Delivers what the payment handlers queued (see OutboxMessage): every few seconds, a batch of
/// due emails, texts and notifications. This is what keeps SMTP and the SMS gateway out of the
/// webhook request — the provider gets its 2xx when the database commits, and the sending
/// happens here, retried on its own schedule if a relay is down.
/// </summary>
public class OutboxProcessorService(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessorService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();
                // Drain a backlog in one tick rather than one batch per tick.
                while (await processor.ProcessDueAsync(BatchSize, stoppingToken) == BatchSize && !stoppingToken.IsCancellationRequested)
                {
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox processing tick failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
