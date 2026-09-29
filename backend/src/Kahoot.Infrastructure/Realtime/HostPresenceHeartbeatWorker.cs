namespace Kahoot.Infrastructure.Realtime;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

internal sealed class HostPresenceHeartbeatWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly HostPresenceService _presence;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<HostPresenceHeartbeatWorker> _logger;

    public HostPresenceHeartbeatWorker(
        HostPresenceService presence,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<HostPresenceHeartbeatWorker> logger)
    {
        _presence = presence;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(Interval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _presence.RenewAllAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Host presence lease renewal failed.");
                }

                try
                {
                    await EvictSuspendedHostsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Suspended Host socket sweep failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task EvictSuspendedHostsAsync(CancellationToken cancellationToken)
    {
        Guid[] hostIds = _presence.GetConnectedHostAccountIds();
        if (hostIds.Length == 0)
        {
            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Dictionary<Guid, int> activeVersions = await dbContext.Users.AsNoTracking()
            .Where(user => hostIds.Contains(user.Id) && user.Status == UserStatus.Active && user.Role == UserRole.Host)
            .ToDictionaryAsync(user => user.Id, user => user.TokenSecurityVersion, cancellationToken);

        _presence.AbortInvalidConnections(activeVersions);
    }
}
