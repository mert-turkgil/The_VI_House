using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.Services;

/// <summary>
/// Background sweep, once an hour: clears the free-text and address fields from membership
/// checkouts that were started but never paid, once they have sat untouched for 30 days.
///
/// A PendingJoin holds everything the /join form asked — job title, full address, "about you",
/// earnings band — for someone who may have decided against it. There is no reason to keep that
/// for a person who never became a member. The row itself stays (name, email, plan, status) so a
/// very late payment on it can still be matched, and so "this address tried to join" is still
/// visible; see MembershipService.PurgeStalePendingJoinsAsync for exactly which fields go.
/// </summary>
public class PendingJoinPurgeService(IServiceScopeFactory scopeFactory, ILogger<PendingJoinPurgeService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetainFor = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var memberships = scope.ServiceProvider.GetRequiredService<IMembershipService>();
                var purged = await memberships.PurgeStalePendingJoinsAsync(RetainFor, stoppingToken);
                if (purged > 0)
                    logger.LogInformation("Cleared form data from {Count} stale pending join(s).", purged);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Same rule as TicketHoldExpiryService: a transient failure is logged and the
                // sweep tries again next tick rather than faulting the host.
                logger.LogError(ex, "Pending join purge sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
