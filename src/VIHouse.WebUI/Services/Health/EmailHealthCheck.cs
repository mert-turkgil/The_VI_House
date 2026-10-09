using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using VIHouse.Business.Options;

namespace VIHouse.WebUI.Services.Health;

/// <summary>
/// Are the SMTP settings plausible? Degraded rather than Unhealthy: the site still serves pages
/// without email, but every confirmation and reset link is silently lost, which is worth a red
/// light on any monitor pointed at /health. Settings only — it never connects to the server, so a
/// monitor polling every minute does not hammer the mailbox.
/// </summary>
public class EmailHealthCheck(IOptionsMonitor<SmtpOptions> smtp) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var problems = smtp.CurrentValue.Problems();
        return Task.FromResult(problems.Count == 0
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Degraded("Email settings need attention — see Admin › Emails."));
    }
}
