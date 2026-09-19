using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.Services;

/// <summary>
/// Hourly: memberships whose paid period has ended become Expired, the account's member status
/// follows, and the member is told once. Access was already refused by the entitlement check the
/// moment ExpiresAt passed; this keeps the rows — and the admin's view of them — honest about it.
/// </summary>
public class MembershipExpirySweepService(IServiceScopeFactory scopeFactory, ILogger<MembershipExpirySweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var memberships = scope.ServiceProvider.GetRequiredService<IMembershipService>();
                var expired = await memberships.ExpireLapsedMembershipsAsync(stoppingToken);
                if (expired > 0)
                    logger.LogInformation("Membership expiry sweep closed {Count} lapsed membership(s).", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Membership expiry sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
