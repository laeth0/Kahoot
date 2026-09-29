namespace Kahoot.Infrastructure.Realtime;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

// Host Presence Heartbeat Worker - Background daemon periodically renewing Redis presence leases for connected hosts and evicting suspended host sockets.
internal sealed class HostPresenceHeartbeatWorker : BackgroundService
{
    // Lease Heartbeat Interval - 10-second cadence safely below 45-second Redis lease window to tolerate transient jitter.
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

    // Periodic Heartbeat Execution Loop - Executes periodic lease renewals and sweeps for invalidated host accounts.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(Interval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // Redis Lease Extension - Renews sliding presence lease for all currently connected hosts.
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
                    // Invalidation Sweep - Queries database to detect suspended hosts or bumped TokenSecurityVersion.
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

    // Active Host Account Validation Sweep - Batches active host IDs across local sockets and queries database with AsNoTracking to verify account status and TokenSecurityVersion.
    private async Task EvictSuspendedHostsAsync(CancellationToken cancellationToken)
    {
        Guid[] hostIds = _presence.GetConnectedHostAccountIds();
        if (hostIds.Length == 0)
        {
            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Host Status and Version Verification - Fetches active TokenSecurityVersion for connected hosts using AsNoTracking.
        Dictionary<Guid, int> activeVersions = await dbContext.Users.AsNoTracking()
            .Where(user => hostIds.Contains(user.Id) && user.Status == UserStatus.Active && user.Role == UserRole.Host)
            .ToDictionaryAsync(user => user.Id, user => user.TokenSecurityVersion, cancellationToken);

        _presence.AbortInvalidConnections(activeVersions);
    }
}
