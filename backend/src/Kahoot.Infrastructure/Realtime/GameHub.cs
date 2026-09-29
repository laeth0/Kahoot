namespace Kahoot.Infrastructure.Realtime;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using MediatR;
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
    private readonly PlayerPresenceService _playerPresence;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GameHub> _logger;

    public GameHub(
        IServiceScopeFactory scopeFactory,
        HostPresenceService presence,
        PlayerPresenceService playerPresence,
        TimeProvider timeProvider,
        ILogger<GameHub> logger)
    {
        _scopeFactory = scopeFactory;
        _presence = presence;
        _playerPresence = playerPresence;
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
        if (!alreadyAttached)
        {
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

        if (game.HostGraceExpiresAt.HasValue)
        {
            game.HostGraceExpiresAt = null;
            await dbContext.SaveChangesAsync(Context.ConnectionAborted);
            _logger.LogInformation("Host grace cancelled. GameId={GameId} HostAccountId={HostAccountId}", gameId, hostAccountId);
        }

        await transaction.CommitAsync(Context.ConnectionAborted);
        return new { success = true, data = new { gameId, connected = true }, error = (object?)null };
    }

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
        await using (IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(Context.ConnectionAborted))
        {
            Game? lockedGame = await dbContext.GetGameForUpdateAsync(
                participant.GameId, participant.HostAccountId, Context.ConnectionAborted);
            Participant? currentParticipant = await dbContext.Participants
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == participant.Id && p.GameId == participant.GameId,
                    Context.ConnectionAborted);
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
            try
            {
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
            }
            catch
            {
                await _playerPresence.RemoveAsync(Context.ConnectionId, participant.GameId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, playerGroup);
                throw;
            }
        }

        int connectedCount = await _playerPresence.GetConnectedCountAsync(participant.GameId);
        Game? game = await dbContext.Games.AsNoTracking().FirstOrDefaultAsync(g => g.Id == participant.GameId, Context.ConnectionAborted);
        if (game is not null)
        {
            ParticipantPresenceChangedEvent presenceEvent = new ParticipantPresenceChangedEvent(
                game.Id, game.PresenceVersion, game.ReservedParticipantCount, connectedCount,
                participant.DisplayNickname, participant.SeatNumber, "Reconnected");
            IGameNotificationService notificationService = scope.ServiceProvider.GetRequiredService<IGameNotificationService>();
            await notificationService.PublishParticipantPresenceChangedAsync(
                participant.HostAccountId, game.Id, game.PresenceVersion, presenceEvent, CancellationToken.None);
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

    public async Task<object> Reconnect(string sessionToken)
    {
        if (sessionToken is null || sessionToken.Length != 68 ||
            !sessionToken.StartsWith("pst_", StringComparison.Ordinal) ||
            _presence.TryGetConnection(Context.ConnectionId, out _, out _) ||
            _playerPresence.TryGetConnection(Context.ConnectionId, out _))
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Invalid session token or connection state." } };
        }

        byte[] presentedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(sessionToken));
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        ParticipantSessionToken? initialToken = await dbContext.ParticipantSessionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TokenHash == presentedTokenHash, Context.ConnectionAborted);
        if (initialToken is null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Invalid session token." } };
        }

        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(Context.ConnectionAborted);
        Game? game = await dbContext.GetGameForUpdateAsync(
            initialToken.GameId, initialToken.HostAccountId, Context.ConnectionAborted);
        if (game is null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Invalid session token." } };
        }

        ParticipantSessionToken? currentToken = await dbContext.ParticipantSessionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.ParticipantId == initialToken.ParticipantId &&
                                      t.TokenHash == presentedTokenHash,
                Context.ConnectionAborted);
        if (currentToken is null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Invalid or revoked session token." } };
        }

        Participant? participant = await dbContext.Participants
            .FirstOrDefaultAsync(p => p.Id == currentToken.ParticipantId && p.GameId == game.Id &&
                                      p.HostAccountId == game.HostAccountId,
                Context.ConnectionAborted);
        if (participant is null || participant.IsRemoved)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.ParticipantRemoved", description = "Participant was removed." } };
        }

        if (currentToken.RevokedAt is not null)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Session token was revoked." } };
        }

        bool hostIsActive = await dbContext.Users.AsNoTracking().AnyAsync(
            user => user.Id == game.HostAccountId && user.Role == UserRole.Host &&
                    user.Status == UserStatus.Active,
            Context.ConnectionAborted);
        if (!hostIsActive)
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.Unavailable", description = "Game host is inactive." } };
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (game.Status == GameStatus.Finished &&
            (game.FinishedAt is null || now >= game.FinishedAt.Value.AddHours(24)))
        {
            return new { success = false, data = (object?)null, error = new { code = "Game.InvalidSessionToken", description = "Session token expired." } };
        }

        participant.ConnectionGeneration += 1;
        await dbContext.SaveChangesAsync(Context.ConnectionAborted);

        string playerGroup = $"host:{game.HostAccountId}:game:{game.Id}:players";
        await Groups.AddToGroupAsync(Context.ConnectionId, playerGroup, Context.ConnectionAborted);
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
        }
        catch
        {
            await _playerPresence.RemoveAsync(Context.ConnectionId, game.Id);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, playerGroup);
            throw;
        }

        _playerPresence.AbortStaleParticipantConnections(participant.Id, participant.ConnectionGeneration);
        try
        {
            await _playerPresence.FenceParticipantAsync(
                participant.Id, game.Id, participant.ConnectionGeneration, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Player socket fencing publication failed. ParticipantId={ParticipantId}", participant.Id);
        }

        int connectedCount = await _playerPresence.GetConnectedCountAsync(game.Id);
        ParticipantPresenceChangedEvent presenceEvent = new ParticipantPresenceChangedEvent(
            game.Id, game.PresenceVersion, game.ReservedParticipantCount, connectedCount,
            participant.DisplayNickname, participant.SeatNumber, "Reconnected");
        IGameNotificationService notificationService = scope.ServiceProvider.GetRequiredService<IGameNotificationService>();
        await notificationService.PublishParticipantPresenceChangedAsync(
            game.HostAccountId, game.Id, game.PresenceVersion, presenceEvent, CancellationToken.None);

        object catchUpData = await BuildPlayerCatchUpStateAsync(dbContext, game, participant, now);
        return new { success = true, data = catchUpData, error = (object?)null };
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            if (_presence.TryGetConnection(Context.ConnectionId, out Guid hostAccountId, out Guid gameId))
            {
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

                if (game is not null)
                {
                    ParticipantPresenceChangedEvent presenceEvent = new ParticipantPresenceChangedEvent(
                        playerInfo.GameId,
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

    private async Task<object> BuildPlayerCatchUpStateAsync(
        AppDbContext dbContext,
        Game game,
        Participant participant,
        DateTimeOffset now)
    {
        if (game.Status == GameStatus.Lobby)
        {
            return new
            {
                status = "LOBBY",
                gameId = game.Id,
                stateVersion = game.StateVersion,
                title = game.Title,
                nickname = participant.DisplayNickname,
                seatNumber = participant.SeatNumber,
                totalParticipants = game.ReservedParticipantCount
            };
        }

        if (game.Status == GameStatus.QuestionActive)
        {
            GameQuestionSnapshot? activeQuestion = await dbContext.GameQuestionSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.GameId == game.Id && q.OrderIndex == game.CurrentQuestionIndex, Context.ConnectionAborted);

            int totalQuestions = await dbContext.GameQuestionSnapshots
                .CountAsync(q => q.GameId == game.Id, Context.ConnectionAborted);

            List<object> choicesList = new List<object>();
            bool alreadyAnswered = false;

            if (activeQuestion is not null)
            {
                List<GameChoiceSnapshot> choiceSnapshots = await dbContext.GameChoiceSnapshots
                    .AsNoTracking()
                    .Where(c => c.GameQuestionId == activeQuestion.Id)
                    .OrderBy(c => c.OrderIndex)
                    .ToListAsync(Context.ConnectionAborted);

                foreach (GameChoiceSnapshot choice in choiceSnapshots)
                {
                    choicesList.Add(new
                    {
                        id = choice.Id,
                        text = choice.Text,
                        orderIndex = choice.OrderIndex
                    });
                }

                alreadyAnswered = await dbContext.AnswerSubmissions
                    .AnyAsync(a => a.GameId == game.Id && a.GameQuestionId == activeQuestion.Id && a.ParticipantId == participant.Id, Context.ConnectionAborted);
            }

            int remainingSeconds = activeQuestion?.EndsAt is not null
                ? Math.Max(0, (int)Math.Ceiling((activeQuestion.EndsAt.Value - now).TotalSeconds))
                : 0;

            return new
            {
                status = "QUESTION_ACTIVE",
                gameId = game.Id,
                stateVersion = game.StateVersion,
                questionIndex = game.CurrentQuestionIndex ?? 1,
                totalQuestions,
                questionText = activeQuestion?.Text ?? string.Empty,
                imageUrl = activeQuestion?.ImageUrl,
                deadlineUtc = activeQuestion?.EndsAt,
                remainingSeconds,
                alreadyAnswered,
                choices = choicesList,
                totalScore = participant.TotalScore
            };
        }

        if (game.Status == GameStatus.QuestionResults)
        {
            GameQuestionSnapshot? currentQuestion = await dbContext.GameQuestionSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.GameId == game.Id && q.OrderIndex == game.CurrentQuestionIndex, Context.ConnectionAborted);

            int totalQuestions = await dbContext.GameQuestionSnapshots
                .CountAsync(q => q.GameId == game.Id, Context.ConnectionAborted);

            List<object> choicesWithResults = new List<object>();
            List<Guid> correctChoiceIds = new List<Guid>();

            if (currentQuestion is not null)
            {
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

                    choicesWithResults.Add(new
                    {
                        id = choice.Id,
                        text = choice.Text,
                        orderIndex = choice.OrderIndex,
                        isCorrect = choice.IsCorrect,
                        selectionCount = choice.SelectionCount
                    });
                }
            }

            AnswerSubmission? submission = null;
            if (currentQuestion is not null)
            {
                submission = await dbContext.AnswerSubmissions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.GameId == game.Id && a.GameQuestionId == currentQuestion.Id && a.ParticipantId == participant.Id, Context.ConnectionAborted);
            }

            List<Guid> participantSelectedChoiceIds = new List<Guid>();
            if (submission is not null)
            {
                participantSelectedChoiceIds = await dbContext.AnswerSubmissionChoices
                    .AsNoTracking()
                    .Where(sc => sc.AnswerSubmissionId == submission.Id)
                    .Select(sc => sc.GameChoiceId)
                    .ToListAsync(Context.ConnectionAborted);
            }

            return new
            {
                status = "QUESTION_RESULTS",
                gameId = game.Id,
                stateVersion = game.StateVersion,
                questionIndex = game.CurrentQuestionIndex ?? 1,
                totalQuestions,
                questionText = currentQuestion?.Text ?? string.Empty,
                choices = choicesWithResults,
                correctChoiceIds,
                submitted = submission is not null,
                isCorrect = submission?.IsCorrect ?? false,
                pointsAwarded = submission?.PointsAwarded ?? 0,
                selectedChoiceIds = participantSelectedChoiceIds,
                totalScore = participant.TotalScore,
                rank = participant.Rank
            };
        }

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

            List<object> leaderboard = new List<object>();
            foreach (Participant topPlayer in top5Participants)
            {
                leaderboard.Add(new
                {
                    participantId = topPlayer.Id,
                    nickname = topPlayer.DisplayNickname,
                    seatNumber = topPlayer.SeatNumber,
                    totalScore = topPlayer.TotalScore,
                    rank = topPlayer.Rank
                });
            }

            return new
            {
                status = "LEADERBOARD",
                gameId = game.Id,
                stateVersion = game.StateVersion,
                totalScore = participant.TotalScore,
                rank = participant.Rank,
                topParticipants = leaderboard
            };
        }

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

            List<object> podium = new List<object>();
            foreach (Participant podiumPlayer in podiumParticipants)
            {
                podium.Add(new
                {
                    participantId = podiumPlayer.Id,
                    nickname = podiumPlayer.DisplayNickname,
                    seatNumber = podiumPlayer.SeatNumber,
                    totalScore = podiumPlayer.TotalScore,
                    rank = podiumPlayer.Rank
                });
            }

            int acceptedAnswerCount = await dbContext.AnswerSubmissions
                .CountAsync(a => a.GameId == game.Id && a.ParticipantId == participant.Id, Context.ConnectionAborted);

            return new
            {
                status = "FINISHED",
                gameId = game.Id,
                stateVersion = game.StateVersion,
                totalScore = participant.TotalScore,
                rank = participant.Rank,
                acceptedAnswers = acceptedAnswerCount,
                podium
            };
        }

        return new
        {
            status = game.Status.ToString().ToUpperInvariant(),
            gameId = game.Id,
            stateVersion = game.StateVersion,
            totalScore = participant.TotalScore,
            rank = participant.Rank
        };
    }
}
