using VIHouse.Business.Abstract;

namespace VIHouse.WebUI.Services;

/// <summary>
/// The safety net under the webhook: every few minutes, any checkout still open on our side that
/// the provider should have an answer for by now is read from the provider and settled through
/// the same path a webhook takes (see ICheckoutReconciliationService). Catches a delivery that
/// never arrived — a host that was down for the retry window, an endpoint misconfigured for a
/// day — without anyone noticing a member who paid and got nothing.
/// </summary>
public class CheckoutReconciliationSweepService(IServiceScopeFactory scopeFactory, ILogger<CheckoutReconciliationSweepService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var reconciliation = scope.ServiceProvider.GetRequiredService<ICheckoutReconciliationService>();
                var read = await reconciliation.ReconcileStaleAsync(stoppingToken);
                if (read > 0)
                    logger.LogInformation("Reconciliation sweep read {Count} open checkout(s) from the provider.", read);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Checkout reconciliation sweep failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
