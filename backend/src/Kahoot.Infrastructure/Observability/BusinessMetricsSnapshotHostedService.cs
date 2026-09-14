using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Observability;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kahoot.Infrastructure.Observability;

public sealed class BusinessMetricsSnapshotHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKahootTelemetry _telemetry;
    private readonly ILogger<BusinessMetricsSnapshotHostedService> _logger;
    private readonly TimeSpan _interval;

    public BusinessMetricsSnapshotHostedService(
        IServiceScopeFactory scopeFactory,
        IKahootTelemetry telemetry,
        ILogger<BusinessMetricsSnapshotHostedService> logger,
        TimeSpan interval)
    {
        _scopeFactory = scopeFactory;
        _telemetry = telemetry;
        _logger = logger;
        _interval = interval;
    }

    public BusinessMetricsSnapshotHostedService(
        IServiceScopeFactory scopeFactory,
        IKahootTelemetry telemetry,
        ILogger<BusinessMetricsSnapshotHostedService> logger,
        IConfiguration configuration)
        : this(
            scopeFactory,
            telemetry,
            logger,
            TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int?>("Observability:BusinessSnapshotIntervalSeconds") ?? 30, 15, 300)))
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        try
        {
            await CollectSnapshotAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CollectSnapshotAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CollectSnapshotAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var activeGames = await dbContext.GameSessions
                .AsNoTracking()
                .Where(g => g.Status != GameStatus.Finished)
                .GroupBy(g => g.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count, stoppingToken);

            int connectedPlayers = await dbContext.Participants
                .AsNoTracking()
                .CountAsync(p => p.ConnectionId != null && !p.IsRemoved, stoppingToken);

            _telemetry.UpdateBusinessSnapshot(activeGames, connectedPlayers);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect business metrics snapshot.");
            _telemetry.RecordSnapshotFailure();
        }
    }
}
