using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.Services;

/// <summary>
/// Background sweep (brief §177-179): every 60s, returns inventory for any TicketHold whose 15-minute
/// window lapsed without a completed checkout — the safety net behind PaymentService's immediate
/// release on checkout failure/expiry webhooks.
/// </summary>
public class TicketHoldExpiryService(IServiceScopeFactory scopeFactory, ILogger<TicketHoldExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var capacity = scope.ServiceProvider.GetRequiredService<ICapacityService>();
                var released = await capacity.ReleaseExpiredHoldsAsync(stoppingToken);
                if (released > 0)
                    logger.LogInformation("Released {Count} expired ticket hold(s) back to inventory.", released);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A transient DB blip here must never take the sweep down permanently — log and
                // retry on the next tick rather than letting the BackgroundService fault out.
                logger.LogError(ex, "Ticket hold expiry sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
