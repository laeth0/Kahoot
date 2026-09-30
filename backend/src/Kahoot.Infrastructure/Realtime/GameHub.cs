namespace Kahoot.Infrastructure.Realtime;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Application.Features.Games.SubmitAnswer;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Realtime Game Hub - Bi-directional WebSocket endpoint orchestrating host session control, anonymous player joins, connection generation fencing, and state synchronization.
public sealed class GameHub : Hub
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HostPresenceService _presence;
    private readonly PlayerPresenceService _playerPresence;
    private readonly UnauthenticatedSocketGuard _unauthenticatedGuard;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        IServiceScopeFactory scopeFactory,
        HostPresenceService presence,
        PlayerPresenceService playerPresence,
        UnauthenticatedSocketGuard unauthenticatedGuard,
        IHostApplicationLifetime lifetime,
        TimeProvider timeProvider,
        ILogger<GameHub> logger)
    {
        _scopeFactory = scopeFactory;
        _presence = presence;
        _playerPresence = playerPresence;
        _unauthenticatedGuard = unauthenticatedGuard;
        _lifetime = lifetime;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // Socket Connection Lifecycle - Tracks unauthenticated socket and initializes 15-second handshake abandonment timer (RT-FAIL-001, RT-RISK-004, RT-TEST-009).
    public override async Task OnConnectedAsync()
    {
        // Graceful Rolling Shutdown Gate (OPS-SHUT-001 item 2) - Ceases accepting new WebSocket connections once SIGTERM drain begins
        if (_lifetime.ApplicationStopping.IsCancellationRequested)
        {
            Context.Abort();
            return;
        }

        _unauthenticatedGuard.Track(Context.ConnectionId, Context.Abort);
        await base.OnConnectedAsync();
    }

    // Host Hub Authorization (RT-METH-004, RT-FAIL-001, RT-TEST-004) - Validates host claims, tenant game ownership, and active credentials; severs socket on authorization failure.
    public async Task<object> JoinAsHost(string gameIdString)
    {
        // Host Role and Authentication Verification (RT-TEST-004) - Verifies caller identity presents authenticated Host role claims.
        if (Context.User?.Identity?.IsAuthenticated != true || !Context.User.IsInRole(nameof(UserRole.Host)))
        {
            TerminateSocket(Context);
            return new { success = false, data = (object?)null, error = new { code = "Auth.Forbidden", description = "The caller is not authorized as a Host." } };
        }

        if (!Guid.TryParse(gameIdString, out Guid gameId))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Invalid gameId format." } };
        }

        // Host Identity Claims Extraction - Resolves host account ID and TokenSecurityVersion from authenticated bearer token claims.
        string? subject = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out Guid hostAccountId) ||
            !int.TryParse(Context.User?.FindFirstValue("token_security_version"), out int tokenVersion))
        {
            TerminateSocket(Context);
            return new { success = false, data = (object?)null, error = new { code = "Auth.Forbidden", description = "Missing or invalid host authentication claims." } };
        }

        // Connection Mutual Exclusion - Enforces that a single socket connection cannot simultaneously bind to both host and player presence maps.
        if (_playerPresence.TryGetConnection(Context.ConnectionId, out _))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Connection is already attached to a game." } };
        }

        bool alreadyAttached = _presence.TryGetConnection(
            Context.ConnectionId, out Guid existingHostId, out Guid existingGameId);
        if (alreadyAttached && (existingHostId != hostAccountId || existingGameId != gameId))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Connection is already attached to another game." } };
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(Context.ConnectionAborted);
        // Pessimistic Game State Lock - Acquires SELECT FOR UPDATE row lock on game record to serialize concurrent host reconnection attempts.
        Game? game = await dbContext.GetGameForUpdateAsync(gameId, hostAccountId, Context.ConnectionAborted);

        if (game is null)
        {
            // A tenant-scoped lookup cannot distinguish a missing game from another host's game.
            TerminateSocket(Context);
            return new { success = false, data = (object?)null, error = new { code = "Auth.Forbidden", description = "The specified game is unavailable to this host." } };
        }

        if (game.Status == GameStatus.Finished)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidStateTransition", description = "Game is finished." } };
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidStateTransition", description = "Host reconnection grace has expired." } };
        }

        // Token Security Invalidation Check - Verifies host account remains Active and TokenSecurityVersion matches database state to evict suspended hosts.
        bool hostIsActive = await dbContext.Users.AsNoTracking().AnyAsync(
            user => user.Id == hostAccountId && user.Role == UserRole.Host &&
                    user.Status == UserStatus.Active && user.TokenSecurityVersion == tokenVersion,
            Context.ConnectionAborted);
        if (!hostIsActive)
        {
            TerminateSocket(Context);
            return new { success = false, data = (object?)null, error = new { code = "Auth.Forbidden", description = "Host account is inactive or token has been revoked." } };
        }

        // Realtime Host Audience Group - Scopes SignalR group membership to isolated tenant host channel preventing cross-game broadcast leaks.
        string hostGroup = $"host:{hostAccountId}:game:{gameId}:hosts";
        if (!alreadyAttached)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, hostGroup, Context.ConnectionAborted);
            try
            {
                // Host Presence Registry - Registers active host connection in distributed Redis presence store with lease expiry.
                await _presence.RegisterAsync(Context.ConnectionId, hostAccountId, gameId, tokenVersion, Context.Abort);
            }
            catch
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, hostGroup);
                throw;
            }
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            if (!alreadyAttached)
            {
                await _presence.RemoveAsync(Context.ConnectionId, gameId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, hostGroup);
            }
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidStateTransition", description = "Host reconnection grace has expired." } };
        }

        // Host Grace Period Reset - Clears abandonment deadline upon successful host reconnection to resume game session.
        if (game.HostGraceExpiresAt.HasValue)
        {
            game.HostGraceExpiresAt = null;
            await dbContext.SaveChangesAsync(Context.ConnectionAborted);
            _logger.LogInformation("Host grace cancelled. GameId={GameId} HostAccountId={HostAccountId}", gameId, hostAccountId);
        }

        await transaction.CommitAsync(Context.ConnectionAborted);

        // Handshake Authorization Completed (RT-FAIL-001) - Cancels 15-second handshake abandonment timer upon authoritative host group binding.
        _unauthenticatedGuard.MarkAuthenticated(Context.ConnectionId);

        return new { success = true, data = new { gameId, connected = true }, error = (object?)null };
    }

    // Anonymous Player Entry Point - Authenticates anonymous participants via ephemeral 6-digit PIN and allocates reserved seat numbers.
    public async Task<object> JoinGame(string pin, string nickname, string joinOperationId)
    {
        if (_presence.TryGetConnection(Context.ConnectionId, out _, out _) ||
            _playerPresence.TryGetConnection(Context.ConnectionId, out _))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Connection is already attached to a game." } };
        }

        if (string.IsNullOrWhiteSpace(pin) || string.IsNullOrWhiteSpace(nickname) || !Guid.TryParse(joinOperationId, out Guid joinOpId))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Invalid join parameters." } };
        }

        string ipAddress = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Mediated Join Command Pipeline - Executes JoinGameCommand through MediatR pipeline applying rate limiting, seat allocation, and nickname reservation.
        JoinGameCommand command = new JoinGameCommand(pin, nickname, joinOpId, ipAddress);
        Result<JoinGameResponse> result = await sender.Send(command, Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            return new { success = false, data = (object?)null, error = new { code = result.Error.Code, description = result.Error.Description } };
        }

        JoinGameResponse joinResponse = result.Value;

        if (string.IsNullOrEmpty(joinResponse.PlayerSessionToken))
        {
            return new { success = true, data = joinResponse, error = (object?)null };
        }

        dbContext.ChangeTracker.Clear();

        Participant? participant = await dbContext.Participants
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == joinResponse.ParticipantId, Context.ConnectionAborted);

        if (participant is null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.ParticipantNotFound", description = "Participant not found." } };
        }

        string playerGroup = $"host:{participant.HostAccountId}:game:{participant.GameId}:players";
        string participantGroup = $"host:{participant.HostAccountId}:game:{participant.GameId}:participant:{participant.Id}";
        await using (IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(Context.ConnectionAborted))
        {
            Game? lockedGame = await dbContext.GetGameForUpdateAsync(
                participant.GameId, participant.HostAccountId, Context.ConnectionAborted);
            Participant? currentParticipant = await dbContext.Participants
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == participant.Id && p.GameId == participant.GameId,
                    Context.ConnectionAborted);
            // Player Session Token Hash Verification - Validates presented player session token against SHA-256 hash digest stored in database.
            byte[] presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(joinResponse.PlayerSessionToken));
            bool tokenIsCurrent = await dbContext.ParticipantSessionTokens.AsNoTracking().AnyAsync(
                token => token.ParticipantId == participant.Id && token.TokenHash == presentedHash &&
                         token.RevokedAt == null,
                Context.ConnectionAborted);

            if (lockedGame is null || lockedGame.Status == GameStatus.Finished ||
                currentParticipant is null || currentParticipant.IsRemoved || !tokenIsCurrent)
            {
                return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Player session is no longer active." } };
            }

            bool hostIsActive = await dbContext.Users.AsNoTracking().AnyAsync(
                user => user.Id == participant.HostAccountId && user.Role == UserRole.Host &&
                        user.Status == UserStatus.Active,
                Context.ConnectionAborted);
            if (!hostIsActive ||
                await _playerPresence.HasActiveConnectionAsync(participant.Id, Context.ConnectionAborted))
            {
                return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Player session is already active or unavailable." } };
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, playerGroup, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, participantGroup, Context.ConnectionAborted);
            try
            {
                // Player Generation Fencing Registration - Registers connection in Redis presence registry with connection generation counter to evict stale duplicate connections.
                await _playerPresence.RegisterAsync(
                    Context.ConnectionId,
                    participant.Id,
                    participant.HostAccountId,
                    participant.GameId,
                    participant.DisplayNickname,
                    participant.SeatNumber,
                    currentParticipant.ConnectionGeneration,
                    Context.Abort);
                await transaction.CommitAsync(Context.ConnectionAborted);
                _playerPresence.MarkCommitted(Context.ConnectionId);
            }
            catch
            {
                await _playerPresence.RemoveAsync(Context.ConnectionId, participant.GameId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, playerGroup);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, participantGroup);
                throw;
            }
        }

        // The socket is bound after the transaction commits; post-commit notifications may take time.
        _unauthenticatedGuard.MarkAuthenticated(Context.ConnectionId);

        try
        {
            int connectedCount = await _playerPresence.GetConnectedCountAsync(participant.GameId);
            Game? game = await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(
                g => g.Id == participant.GameId, CancellationToken.None);
            if (game is not null)
            {
                ParticipantPresenceChangedEvent presenceEvent = new ParticipantPresenceChangedEvent(
                    game.Id, game.StateVersion, game.PresenceVersion, game.ReservedParticipantCount, connectedCount,
                    participant.DisplayNickname, participant.SeatNumber, "Joined");
                IGameNotificationService notificationService = scope.ServiceProvider.GetRequiredService<IGameNotificationService>();
                await notificationService.PublishParticipantPresenceChangedAsync(
                    participant.HostAccountId, game.Id, game.PresenceVersion, presenceEvent, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Player presence notification failed after join commit. GameId={GameId}", participant.GameId);
        }

        return new
        {
            success = true,
            data = new
            {
                participantId = joinResponse.ParticipantId,
                playerSessionToken = joinResponse.PlayerSessionToken,
                gameId = joinResponse.GameId,
                nickname = joinResponse.Nickname,
                title = joinResponse.Title,
                seatNumber = joinResponse.SeatNumber
            },
            error = (object?)null
        };
    }

    // Player Reconnection Workflow (RECON-REC-001, RECON-REC-002, RECON-GEN-001) - Restores participant socket binding, bumps generation sequence, fences stale connections, and projects catch-up state.
    public async Task<object> Reconnect(string sessionToken)
    {
        // Session Token Structural Validation (RECON-ERR-001) - Enforces strict 68-character length and 'pst_' prefix format to short-circuit malformed reconnect requests.
        if (sessionToken is null || sessionToken.Length != 68 ||
            !sessionToken.StartsWith("pst_", StringComparison.Ordinal))
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Invalid session token." } };
        }

        // Host sockets cannot be rebound as player sockets.
        if (_presence.TryGetConnection(Context.ConnectionId, out _, out _))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Connection is already attached to a game." } };
        }

        // Session Token Hash Lookup (RECON-STORM-001, RECON-RISK-003) - Resolves participant session record via indexed SHA-256 hash lookup.
        byte[] presentedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken));
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Indexed Token Lookup (RECON-STORM-001) - Executes AsNoTracking indexed query on TokenHash to short-circuit invalid or missing tokens.
        ParticipantSessionToken? initialToken = await dbContext.ParticipantSessionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == presentedTokenHash, Context.ConnectionAborted);
        if (initialToken is null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Invalid session token." } };
        }

        // A lost invocation response can be retried on the same live socket, but that socket cannot switch participants.
        bool alreadyAttached = _playerPresence.TryGetConnection(Context.ConnectionId, out PlayerConnectionInfo existingConnection);
        if (alreadyAttached && (existingConnection.ParticipantId != initialToken.ParticipantId ||
                                existingConnection.GameId != initialToken.GameId))
        {
            return new { success = false, data = (object?)null, error = new { code = "Validation.Failed", description = "Connection is already attached to another player." } };
        }

        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(Context.ConnectionAborted);
        // Lock Wait Bound - Sets 3-second statement lock timeout to eliminate hung transactions under thundering herd storms.
        await dbContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", Context.ConnectionAborted);

        // Shared Host Lock - Keeps suspension from committing between authorization and socket binding.
        List<User> hosts = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {initialToken.HostAccountId} FOR SHARE")
            .AsNoTracking()
            .ToListAsync(Context.ConnectionAborted);
        User? host = hosts.Count == 0 ? null : hosts[0];
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.Unavailable", description = "Game host is inactive." } };
        }

        // Shared Game Lock (RECON-STORM-001, RECON-RISK-003) - Allows different players to reconnect concurrently while excluding game transitions.
        List<Game> games = await dbContext.Games
            .FromSqlInterpolated($"""
                SELECT * FROM games
                WHERE id = {initialToken.GameId} AND host_account_id = {initialToken.HostAccountId}
                FOR SHARE
                """)
            .ToListAsync(Context.ConnectionAborted);
        Game? game = games.Count == 0 ? null : games[0];

        // Game Termination & Availability Check (RECON-ERR-003) - Rejects reconnects if game was terminated by admin or does not exist.
        if (game is null || game.IsTerminatedBySuspension)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.Unavailable", description = "Game was terminated or unavailable." } };
        }

        // Concurrent Token Revocation Check - Re-evaluates token under transaction lock to prevent races with concurrent kick or removal operations.
        ParticipantSessionToken? currentToken = await dbContext.ParticipantSessionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.ParticipantId == initialToken.ParticipantId &&
                                      t.TokenHash == presentedTokenHash,
                Context.ConnectionAborted);
        if (currentToken is null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Invalid or revoked session token." } };
        }

        // Pessimistic Participant Row Lock (RECON-RISK-002, RECON-TEST-007) - Acquires FOR UPDATE lock on participant row to serialize simultaneous reconnects from multiple tabs for the same session.
        List<Participant> participants = await dbContext.Participants
            .FromSqlInterpolated($"""
                SELECT * FROM participants
                WHERE id = {currentToken.ParticipantId} AND game_id = {game.Id} AND host_account_id = {game.HostAccountId}
                FOR UPDATE
                """)
            .ToListAsync(Context.ConnectionAborted);
        Participant? participant = participants.Count == 0 ? null : participants[0];

        // Participant Tombstone Verification (RECON-ERR-002, RECON-RISK-005, RECON-TEST-010) - Instantly rejects kicked or removed players.
        if (participant is null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Invalid session token." } };
        }

        if (participant.IsRemoved)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.ParticipantRemoved", description = "Participant was removed." } };
        }

        if (currentToken.RevokedAt is not null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Session token was revoked." } };
        }

        // PostgreSQL provides one clock for the strict expiry boundary across replicas.
        DateTimeOffset now = await dbContext.Database
            .SqlQuery<DateTimeOffset>($"SELECT clock_timestamp() AS \"Value\"")
            .SingleAsync(Context.ConnectionAborted);
        if (game.Status == GameStatus.Finished)
        {
            if (game.FinishedAt is null || now >= game.FinishedAt.Value.AddHours(24))
            {
                return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Session token expired." } };
            }
        }
        else if (currentToken.ExpiresAt is not null && now >= currentToken.ExpiresAt.Value)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Session token expired." } };
        }

        if (game.HostGraceExpiresAt is not null && game.HostGraceExpiresAt.Value <= now && game.Status != GameStatus.Finished)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.Unavailable", description = "Game has been abandoned." } };
        }

        // Connection Generation Monotonic Increment (RECON-GEN-001, RECON-SEC-001) - Bumps connection generation sequence to fence out prior sockets and serialize reconnecting player instances.
        participant.ConnectionGeneration += 1;
        await dbContext.SaveChangesAsync(Context.ConnectionAborted);

        // Authoritative Phase Catch-Up Builder (RECON-CATCH-001) - Builds complete, self-contained state projection under coherent shared snapshot.
        object catchUpData = await BuildPlayerCatchUpStateAsync(dbContext, game, participant);

        string playerGroup = $"host:{game.HostAccountId}:game:{game.Id}:players";
        string participantGroup = $"host:{game.HostAccountId}:game:{game.Id}:participant:{participant.Id}";
        if (alreadyAttached)
        {
            await transaction.CommitAsync(Context.ConnectionAborted);
            _playerPresence.UpdateConnectionGeneration(Context.ConnectionId, participant.Id, participant.ConnectionGeneration);
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, playerGroup, Context.ConnectionAborted);
            await Groups.AddToGroupAsync(Context.ConnectionId, participantGroup, Context.ConnectionAborted);
            try
            {
                await _playerPresence.RegisterAsync(
                    Context.ConnectionId,
                    participant.Id,
                    game.HostAccountId,
                    game.Id,
                    participant.DisplayNickname,
                    participant.SeatNumber,
                    participant.ConnectionGeneration,
                    Context.Abort);
                await transaction.CommitAsync(Context.ConnectionAborted);
                _playerPresence.MarkCommitted(Context.ConnectionId);
            }
            catch
            {
                await _playerPresence.RemoveAsync(Context.ConnectionId, game.Id);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, playerGroup);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, participantGroup);
                throw;
            }
        }

        _unauthenticatedGuard.MarkAuthenticated(Context.ConnectionId);

        // Connection Generation Local Eviction (RECON-GEN-001, RECON-SEC-001) - Aborts any local socket connections for this participant holding an older generation counter.
        _playerPresence.AbortStaleParticipantConnections(participant.Id, participant.ConnectionGeneration);
        try
        {
            // Distributed Connection Generation Fencing (RECON-SEC-001, RECON-TEST-007) - Broadcasts Redis fence frame to abort stale sockets for this participant across all nodes.
            await _playerPresence.FenceParticipantAsync(
                participant.Id, game.Id, participant.ConnectionGeneration, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Player socket fencing publication failed. ParticipantId={ParticipantId}", participant.Id);
        }

        if (!alreadyAttached && game.Status != GameStatus.Finished)
        {
            try
            {
                int connectedCount = await _playerPresence.GetConnectedCountAsync(game.Id);
                ParticipantPresenceChangedEvent presenceEvent = new ParticipantPresenceChangedEvent(
                    game.Id, game.StateVersion, game.PresenceVersion, game.ReservedParticipantCount, connectedCount,
                    participant.DisplayNickname, participant.SeatNumber, "Reconnected");
                IGameNotificationService notificationService = scope.ServiceProvider.GetRequiredService<IGameNotificationService>();
                await notificationService.PublishParticipantPresenceChangedAsync(
                    game.HostAccountId, game.Id, game.PresenceVersion, presenceEvent, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Player presence notification failed after reconnect commit. GameId={GameId}", game.Id);
            }
        }

        return new { success = true, data = catchUpData, error = (object?)null };
    }

    // Participant Answer Submission Hub Method (PLAY-ANS-001) - Ingests choice selection from connected player socket with authoritative timestamping.
    public async Task<object> SubmitAnswer(string questionIdString, List<string> choiceIdStrings)
    {
        // Participant Connection Mapping - Resolves participant identity from active connection registry.
        if (!_playerPresence.TryGetConnection(Context.ConnectionId, out PlayerConnectionInfo playerInfo))
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Player is not connected to an active game session." } };
        }

        // Malformed attempts still reach the participant and socket rate limits.
        Guid.TryParse(questionIdString, out Guid questionId);
        List<Guid> choiceGuids = new List<Guid>();
        if (choiceIdStrings is { Count: > 6 })
        {
            choiceGuids.AddRange(Enumerable.Repeat(Guid.Empty, 7));
        }
        else if (choiceIdStrings is not null)
        {
            foreach (string choiceString in choiceIdStrings)
            {
                Guid.TryParse(choiceString, out Guid choiceGuid);
                choiceGuids.Add(choiceGuid);
            }
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        SubmitAnswerCommand command = new SubmitAnswerCommand(
            playerInfo.GameId,
            playerInfo.ParticipantId,
            questionId,
            choiceGuids,
            Context.ConnectionId,
            null,
            playerInfo.ConnectionGeneration);

        Result<SubmitAnswerResponse> result = await sender.Send(command, Context.ConnectionAborted);

        if (result.IsSuccess)
        {
            return new
            {
                success = true,
                data = new
                {
                    accepted = result.Value.Accepted,
                    alreadyAnswered = result.Value.AlreadyAnswered
                },
                error = (object?)null
            };
        }

        return new
        {
            success = false,
            data = (object?)null,
            error = new
            {
                code = result.Error.Code,
                description = result.Error.Description
            }
        };
    }

    // Socket Disconnect Lifecycle - Manages host abandonment grace lease initiation and player presence departure broadcasting.
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            // Handshake Guard Cleanup - Removes pending handshake abandonment tracking upon socket disconnection.
            _unauthenticatedGuard.Remove(Context.ConnectionId);

            // The lease expires on peers; querying PostgreSQL for every socket during a planned
            // replica drain would amplify shutdown into a database outage.
            if (_lifetime.ApplicationStopping.IsCancellationRequested)
            {
                return;
            }

            // Host Disconnection Handling - Detects loss of host socket and begins 300-second abandonment grace if zero active hosts remain.
            if (_presence.TryGetConnection(Context.ConnectionId, out Guid hostAccountId, out Guid gameId))
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();
                Game? game = await dbContext.GetGameForUpdateAsync(gameId, hostAccountId, CancellationToken.None);

                bool hasRemainingHost = await _presence.RemoveAsync(Context.ConnectionId, gameId);
                if (!hasRemainingHost && game is not null && game.Status != GameStatus.Finished)
                {
                    // Host Grace Period Activation - Initiates 300-second countdown before GameAbandonmentWorker terminates the orphaned game.
                    game.HostGraceExpiresAt = _timeProvider.GetUtcNow().AddSeconds(300);
                    await dbContext.SaveChangesAsync();
                    _logger.LogWarning("Last Host disconnected; abandonment grace started. GameId={GameId} ExpiresAt={ExpiresAt}",
                        gameId, game.HostGraceExpiresAt);
                }

                await transaction.CommitAsync();
            }
            // Player Disconnection Handling - Cleans up player presence and emits departure event if no concurrent connections remain.
            else if (_playerPresence.TryGetConnection(Context.ConnectionId, out PlayerConnectionInfo playerInfo))
            {
                await _playerPresence.RemoveAsync(Context.ConnectionId, playerInfo.GameId);
                if (await _playerPresence.HasActiveConnectionAsync(playerInfo.ParticipantId, CancellationToken.None))
                {
                    return;
                }

                int connectedCount = await _playerPresence.GetConnectedCountAsync(playerInfo.GameId);

                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Game? game = await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == playerInfo.GameId);

                if (game is not null && game.Status != GameStatus.Finished)
                {
                    // Player Disconnected Presence Broadcast - Emits updated connected count to host and player channels.
                    ParticipantPresenceChangedEvent presenceEvent = new ParticipantPresenceChangedEvent(
                        playerInfo.GameId,
                        game.StateVersion,
                        game.PresenceVersion,
                        game.ReservedParticipantCount,
                        connectedCount,
                        playerInfo.Nickname,
                        playerInfo.SeatNumber,
                        "Disconnected");

                    IGameNotificationService notificationService = scope.ServiceProvider.GetRequiredService<IGameNotificationService>();
                    await notificationService.PublishParticipantPresenceChangedAsync(
                        playerInfo.HostAccountId,
                        playerInfo.GameId,
                        game.PresenceVersion,
                        presenceEvent,
                        CancellationToken.None);
                }
            }
        }
        catch (Exception disconnectException)
        {
            _logger.LogError(disconnectException, "Disconnect processing failed. ConnectionId={ConnectionId}", Context.ConnectionId);
        }
        finally
        {
            await base.OnDisconnectedAsync(exception);
        }
    }

    // Phase-Specific Catch-Up State Builder (RECON-CATCH-001) - Serializes minimal, secure player state projection corresponding to current game lifecycle phase.
    private async Task<object> BuildPlayerCatchUpStateAsync(
        AppDbContext dbContext,
        Game game,
        Participant participant)
    {
        // Lobby State Catch-Up (RECON-CATCH-001) - Returns participant display metadata and total player reservation count.
        if (game.Status == GameStatus.Lobby || game.Status == GameStatus.Created)
        {
            return new PlayerLobbyStateResponse(
                "LOBBY",
                game.Id,
                game.StateVersion,
                game.Title,
                participant.DisplayNickname,
                participant.SeatNumber,
                game.ReservedParticipantCount);
        }

        // Question Active Catch-Up (RECON-CATCH-001, RECON-SEC-002, RECON-BOUND-002) - Returns active question prompt, choice options, and countdown deadline without leaking correctness flags.
        if (game.Status == GameStatus.QuestionActive)
        {
            GameQuestionSnapshot? activeQuestion = await dbContext.GameQuestionSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.GameId == game.Id && q.OrderIndex == game.CurrentQuestionIndex, Context.ConnectionAborted);

            int totalQuestions = await dbContext.GameQuestionSnapshots
                .CountAsync(q => q.GameId == game.Id, Context.ConnectionAborted);

            if (activeQuestion is null)
            {
                throw new InvalidOperationException("An active game has no current question snapshot.");
            }

            if (activeQuestion.EndsAt is null)
            {
                throw new InvalidOperationException("An active question has no deadline.");
            }

            List<GameChoiceSnapshot> choiceSnapshots = await dbContext.GameChoiceSnapshots
                .AsNoTracking()
                .Where(c => c.GameQuestionId == activeQuestion.Id)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync(Context.ConnectionAborted);

            List<PlayerChoiceSnapshotDto> choicesList = new List<PlayerChoiceSnapshotDto>(choiceSnapshots.Count);
            foreach (GameChoiceSnapshot choice in choiceSnapshots)
            {
                choicesList.Add(new PlayerChoiceSnapshotDto(
                    choice.Id,
                    choice.Text,
                    choice.OrderIndex));
            }

            bool alreadyAnswered = await dbContext.AnswerSubmissions.AsNoTracking()
                .AnyAsync(answer => answer.GameId == game.Id &&
                                    answer.GameQuestionId == activeQuestion.Id &&
                                    answer.ParticipantId == participant.Id, Context.ConnectionAborted);

            // Pre-Reveal Secrecy Guarantee (RECON-SEC-002) - Historical submissions alone determine score visible before reveal; provisional points withheld.
            long revealedTotalScore = await dbContext.AnswerSubmissions.AsNoTracking()
                .Where(answer => answer.GameId == game.Id &&
                                 answer.ParticipantId == participant.Id &&
                                 answer.GameQuestionId != activeQuestion.Id)
                .SumAsync(answer => (long)answer.PointsAwarded, Context.ConnectionAborted);

            // Active Question Timer Clamping (RECON-BOUND-002) - If T > Deadline, remaining seconds evaluates strictly to 0.
            int remainingSeconds = Math.Max(0, (int)Math.Ceiling((activeQuestion.EndsAt.Value - _timeProvider.GetUtcNow()).TotalSeconds));

            return new PlayerQuestionActiveStateResponse(
                "QUESTION_ACTIVE",
                game.Id,
                game.StateVersion,
                activeQuestion.OrderIndex - 1,
                totalQuestions,
                activeQuestion.Text,
                activeQuestion.ImageUrl,
                activeQuestion.EndsAt,
                remainingSeconds,
                alreadyAnswered,
                choicesList,
                revealedTotalScore);
        }

        // Question Results Catch-Up (RECON-CATCH-001) - Reveals correct choice IDs, answer breakdown counts, participant selection, and points awarded.
        if (game.Status == GameStatus.QuestionResults)
        {
            GameQuestionSnapshot? currentQuestion = await dbContext.GameQuestionSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.GameId == game.Id && q.OrderIndex == game.CurrentQuestionIndex, Context.ConnectionAborted);

            int totalQuestions = await dbContext.GameQuestionSnapshots
                .CountAsync(q => q.GameId == game.Id, Context.ConnectionAborted);

            if (currentQuestion is null)
            {
                throw new InvalidOperationException("A results phase has no current question snapshot.");
            }

            List<QuestionChoiceResultDto> choicesWithResults = new List<QuestionChoiceResultDto>();
            List<Guid> correctChoiceIds = new List<Guid>();

            List<GameChoiceSnapshot> choiceSnapshots = await dbContext.GameChoiceSnapshots
                .AsNoTracking()
                .Where(c => c.GameQuestionId == currentQuestion.Id)
                .OrderBy(c => c.OrderIndex)
                .ToListAsync(Context.ConnectionAborted);

            foreach (GameChoiceSnapshot choice in choiceSnapshots)
            {
                if (choice.IsCorrect)
                {
                    correctChoiceIds.Add(choice.Id);
                }

                choicesWithResults.Add(new QuestionChoiceResultDto(
                    choice.Id,
                    choice.OrderIndex,
                    choice.Text,
                    choice.IsCorrect,
                    choice.SelectionCount));
            }

            AnswerSubmission? submission = await dbContext.AnswerSubmissions
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.GameId == game.Id && a.GameQuestionId == currentQuestion.Id && a.ParticipantId == participant.Id, Context.ConnectionAborted);

            List<Guid> participantSelectedChoiceIds = new List<Guid>();
            if (submission is not null)
            {
                participantSelectedChoiceIds = await dbContext.AnswerSubmissionChoices
                    .AsNoTracking()
                    .Where(sc => sc.AnswerSubmissionId == submission.Id)
                    .Select(sc => sc.GameChoiceId)
                    .ToListAsync(Context.ConnectionAborted);
            }

            return new PlayerQuestionResultsStateResponse(
                "QUESTION_RESULTS",
                game.Id,
                game.StateVersion,
                (game.CurrentQuestionIndex ?? 1) - 1,
                totalQuestions,
                currentQuestion.Text,
                currentQuestion.ImageUrl,
                choicesWithResults,
                correctChoiceIds,
                submission is not null,
                submission?.IsCorrect ?? false,
                submission?.PointsAwarded ?? 0,
                participantSelectedChoiceIds,
                participant.TotalScore);
        }

        // Leaderboard Catch-Up (RECON-CATCH-001) - Queries top 5 scored participants with deterministic tie-breaking and reports personal sequential rank.
        if (game.Status == GameStatus.Leaderboard)
        {
            List<Participant> top5Participants = await dbContext.Participants
                .AsNoTracking()
                .Where(p => p.GameId == game.Id && !p.IsRemoved)
                .OrderByDescending(p => p.TotalScore)
                .ThenBy(p => p.NormalizedNickname)
                .ThenBy(p => p.Id)
                .Take(5)
                .ToListAsync(Context.ConnectionAborted);

            List<LeaderboardPlayerDto> leaderboard = new List<LeaderboardPlayerDto>(top5Participants.Count);
            foreach (Participant topPlayer in top5Participants)
            {
                leaderboard.Add(new LeaderboardPlayerDto(
                    topPlayer.Id,
                    topPlayer.DisplayNickname,
                    topPlayer.SeatNumber,
                    topPlayer.TotalScore,
                    topPlayer.Rank));
            }

            int rank = participant.Rank ?? throw new InvalidOperationException("A leaderboard participant has no materialized rank.");

            return new PlayerLeaderboardStateResponse(
                "LEADERBOARD",
                game.Id,
                game.StateVersion,
                participant.TotalScore,
                rank,
                leaderboard);
        }

        // Finished Game Podium Catch-Up (RECON-CATCH-001, RECON-WINDOW-001, RECON-TEST-004) - Materializes top 3 podium participants, final sequential rank, and total accepted answers.
        if (game.Status == GameStatus.Finished)
        {
            List<Participant> podiumParticipants = await dbContext.Participants
                .AsNoTracking()
                .Where(p => p.GameId == game.Id && !p.IsRemoved)
                .OrderByDescending(p => p.TotalScore)
                .ThenBy(p => p.NormalizedNickname)
                .ThenBy(p => p.Id)
                .Take(3)
                .ToListAsync(Context.ConnectionAborted);

            List<PodiumPlayerDto> podium = new List<PodiumPlayerDto>(podiumParticipants.Count);
            foreach (Participant podiumPlayer in podiumParticipants)
            {
                podium.Add(new PodiumPlayerDto(
                    podiumPlayer.Id,
                    podiumPlayer.DisplayNickname,
                    podiumPlayer.SeatNumber,
                    podiumPlayer.TotalScore,
                    podiumPlayer.Rank));
            }

            int acceptedAnswerCount = await dbContext.AnswerSubmissions
                .CountAsync(a => a.GameId == game.Id && a.ParticipantId == participant.Id, Context.ConnectionAborted);

            int rank = participant.Rank ?? throw new InvalidOperationException("A finished participant has no materialized rank.");

            return new PlayerFinishedStateResponse(
                "FINISHED",
                game.Id,
                game.StateVersion,
                participant.TotalScore,
                rank,
                acceptedAnswerCount,
                podium);
        }

        throw new InvalidOperationException($"Unsupported player catch-up state: {game.Status}.");
    }

    // Socket Eviction Dispatcher (RT-FAIL-001) - Schedules asynchronous socket abort to allow error response frame transmission before connection severance.
    private static void TerminateSocket(HubCallerContext context)
    {
        Task.Run(async () =>
        {
            await Task.Delay(100);
            context.Abort();
        });
    }
}
