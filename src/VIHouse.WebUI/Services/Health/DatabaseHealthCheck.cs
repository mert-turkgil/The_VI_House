using Microsoft.Extensions.Diagnostics.HealthChecks;
using VIHouse.DataAccess.Concrete.EntityFramework;

namespace VIHouse.WebUI.Services.Health;

/// <summary>Can the app reach its database? The one dependency without which nothing works.</summary>
public class DatabaseHealthCheck(VIHouseDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cannot connect to the database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check threw.", ex);
        }
    }
}
