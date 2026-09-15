using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kahoot.Infrastructure.Authentication;

public sealed class TokenCleanupHostedService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<TokenCleanupHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);
    private const int BatchLimit = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval, timeProvider);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupTokensAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "An error occurred while cleaning up expired and revoked refresh tokens.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupTokensAsync(CancellationToken cancellationToken)
    {
        DateTime cutoff = timeProvider.GetUtcNow().Subtract(RetentionPeriod).UtcDateTime;

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        KahootDbContext dbContext = scope.ServiceProvider.GetRequiredService<KahootDbContext>();

        var expiredIds = await dbContext.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff || (t.RevokedAt != null && t.RevokedAt < cutoff))
            .OrderBy(t => t.ExpiresAt)
            .Select(t => t.Id)
            .Take(BatchLimit)
            .ToListAsync(cancellationToken);

        if (expiredIds.Count == 0)
        {
            return;
        }

        int deleted = await dbContext.RefreshTokens
            .Where(t => expiredIds.Contains(t.Id))
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Cleaned up {Count} expired or revoked refresh tokens.", deleted);
    }
}
