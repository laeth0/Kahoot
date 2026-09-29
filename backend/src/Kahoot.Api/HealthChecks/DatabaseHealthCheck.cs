using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;

namespace Kahoot.Api.HealthChecks;

// Database Health Check - Validates PostgreSQL connectivity for infrastructure liveness and readiness probe orchestration.
internal sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    // Health Check Execution - Probes database socket connectivity while suppressing distributed tracing overhead.
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Trace Suppression - Suppresses OpenTelemetry spans during frequent readiness polls to prevent telemetry saturation.
        using (SuppressInstrumentationScope.Begin())
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
        }
    }
}
