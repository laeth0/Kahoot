namespace Kahoot.Infrastructure.Realtime;

using System.Security.Claims;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class GameHub : Hub
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HostPresenceService _presence;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        IServiceScopeFactory scopeFactory,
        HostPresenceService presence,
        TimeProvider timeProvider,
        ILogger<GameHub> logger)
    {
        _scopeFactory = scopeFactory;
        _presence = presence;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [Authorize(Roles = nameof(UserRole.Host))]
    public async Task<object> JoinAsHost(string gameIdString)
    {
        if (!Guid.TryParse(gameIdString, out Guid gameId))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Invalid gameId format." } };
        }

        string? subject = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out Guid hostAccountId) ||
            !int.TryParse(Context.User?.FindFirstValue("token_security_version"), out int tokenVersion))
        {
            return new { success = false, data = (object?)null, error = new { code = "Auth.Unauthorized", description = "Unauthorized." } };
        }

        if (_presence.TryGetConnection(Context.ConnectionId, out Guid existingHostId, out Guid existingGameId) &&
            (existingHostId != hostAccountId || existingGameId != gameId))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Connection is already attached to another game." } };
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(Context.ConnectionAborted);
        Game? game = await dbContext.GetGameForUpdateAsync(gameId, hostAccountId, Context.ConnectionAborted);

        if (game is null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.NotFound", description = "Game not found." } };
        }

        if (game.Status == GameStatus.Finished)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidStateTransition", description = "Game is finished." } };
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidStateTransition", description = "Host reconnection grace has expired." } };
        }

        bool hostIsActive = await dbContext.Users.AsNoTracking().AnyAsync(
            user => user.Id == hostAccountId && user.Role == UserRole.Host &&
                    user.Status == UserStatus.Active && user.TokenSecurityVersion == tokenVersion,
            Context.ConnectionAborted);
        if (!hostIsActive)
        {
            return new { success = false, data = (object?)null, error = new { code = "Auth.Unauthorized", description = "Unauthorized." } };
        }

        string hostGroup = $"host:{hostAccountId}:game:{gameId}:hosts";
        await Groups.AddToGroupAsync(Context.ConnectionId, hostGroup, Context.ConnectionAborted);
        try
        {
            await _presence.RegisterAsync(Context.ConnectionId, hostAccountId, gameId, tokenVersion, Context.Abort);
        }
        catch
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, hostGroup);
            throw;
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            await _presence.RemoveAsync(Context.ConnectionId, gameId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, hostGroup);
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidStateTransition", description = "Host reconnection grace has expired." } };
        }

        if (game.HostGraceExpiresAt.HasValue)
        {
            game.HostGraceExpiresAt = null;
            await dbContext.SaveChangesAsync(Context.ConnectionAborted);
            _logger.LogInformation("Host grace cancelled. GameId={GameId} HostAccountId={HostAccountId}", gameId, hostAccountId);
        }

        await transaction.CommitAsync(Context.ConnectionAborted);
        return new { success = true, data = new { gameId, connected = true }, error = (object?)null };
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (!_presence.TryGetConnection(Context.ConnectionId, out Guid hostAccountId, out Guid gameId))
            {
                return;
            }

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
            Game? game = await dbContext.GetGameForUpdateAsync(gameId, hostAccountId, CancellationToken.None);

            bool hasRemainingHost = await _presence.RemoveAsync(Context.ConnectionId, gameId);
            if (!hasRemainingHost && game is not null && game.Status != GameStatus.Finished)
            {
                game.HostGraceExpiresAt = _timeProvider.GetUtcNow().AddSeconds(300);
                await dbContext.SaveChangesAsync();
                _logger.LogWarning("Last Host disconnected; abandonment grace started. GameId={GameId} ExpiresAt={ExpiresAt}",
                    gameId, game.HostGraceExpiresAt);
            }

            await transaction.CommitAsync();
        }
        catch (Exception disconnectException)
        {
            _logger.LogError(disconnectException, "Host disconnect processing failed. ConnectionId={ConnectionId}", Context.ConnectionId);
        }
        finally
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}
