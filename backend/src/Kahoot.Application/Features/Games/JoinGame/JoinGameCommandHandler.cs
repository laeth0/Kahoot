namespace Kahoot.Application.Features.Games.JoinGame;

using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public sealed class JoinGameCommandHandler : ICommandHandler<JoinGameCommand, JoinGameResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ILobbyJoinRateLimiter _rateLimiter;
    private readonly IPlayerPresenceService _playerPresenceService;
    private readonly IGameNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JoinGameCommandHandler> _logger;

    public JoinGameCommandHandler(
        IAppDbContext dbContext,
        ILobbyJoinRateLimiter rateLimiter,
        IPlayerPresenceService playerPresenceService,
        IGameNotificationService notificationService,
        TimeProvider timeProvider,
        ILogger<JoinGameCommandHandler> logger)
    {
        _dbContext = dbContext;
        _rateLimiter = rateLimiter;
        _playerPresenceService = playerPresenceService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<JoinGameResponse>> Handle(
        JoinGameCommand request,
        CancellationToken cancellationToken)
    {
        if (await _rateLimiter.IsRateLimitedAsync(request.IpAddress, cancellationToken))
        {
            return Result.Failure<JoinGameResponse>(GameErrors.RateLimited);
        }

        if (!PlayerNickname.TryNormalize(request.Nickname, out string displayNickname, out string normalizedNickname))
        {
            return Result.Failure<JoinGameResponse>(Error.Validation("Validation.Failed", "Invalid nickname."));
        }
        byte[] joinOperationIdHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.JoinOperationId.ToString("D").ToLowerInvariant()));

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(
            cancellationToken);

        Participant? priorOperation = await _dbContext.Participants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.JoinOperationIdHash == joinOperationIdHash, cancellationToken);
        if (priorOperation is not null)
        {
            string? originalPin = await _dbContext.Games.AsNoTracking()
                .Where(g => g.Id == priorOperation.GameId)
                .Select(g => g.Pin)
                .FirstOrDefaultAsync(cancellationToken);
            if (originalPin != request.Pin)
            {
                return Result.Failure<JoinGameResponse>(Error.Validation(
                    "Validation.Failed", "Mismatched join operation parameters."));
            }
        }

        Game? game = await _dbContext.GetGameByPinForUpdateAsync(request.Pin, cancellationToken);
        if (game is null)
        {
            return Result.Failure<JoinGameResponse>(GameErrors.InvalidPin);
        }

        if (game.Status != GameStatus.Lobby)
        {
            return Result.Failure<JoinGameResponse>(GameErrors.NotJoinable);
        }

        bool hostIsActive = await _dbContext.Users.AsNoTracking().AnyAsync(
            user => user.Id == game.HostAccountId && user.Role == UserRole.Host &&
                    user.Status == UserStatus.Active,
            cancellationToken);
        if (!hostIsActive)
        {
            return Result.Failure<JoinGameResponse>(GameErrors.NotJoinable);
        }

        Participant? existingParticipant = await _dbContext.Participants
            .FirstOrDefaultAsync(
                p => p.GameId == game.Id && p.JoinOperationIdHash == joinOperationIdHash,
                cancellationToken);

        if (existingParticipant is not null)
        {
            if (existingParticipant.NormalizedNickname != normalizedNickname)
            {
                return Result.Failure<JoinGameResponse>(Error.Validation("Validation.Failed", "Mismatched join operation parameters."));
            }

            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (now >= existingParticipant.JoinRecoveryExpiresAt)
            {
                return Result.Failure<JoinGameResponse>(GameErrors.NotJoinable);
            }

            if (existingParticipant.IsRemoved)
            {
                return Result.Failure<JoinGameResponse>(GameErrors.NicknameTaken);
            }

            bool hasActiveSocket = await _playerPresenceService.HasActiveConnectionAsync(existingParticipant.Id, cancellationToken);
            string returnedToken;

            if (!hasActiveSocket)
            {
                returnedToken = "pst_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
                ParticipantSessionToken sessionToken = new ParticipantSessionToken
                {
                    Id = Guid.NewGuid(),
                    HostAccountId = game.HostAccountId,
                    GameId = game.Id,
                    ParticipantId = existingParticipant.Id,
                    TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(returnedToken)),
                    CreatedAt = now,
                    RevokedAt = null,
                    ExpiresAt = null
                };
                _dbContext.ParticipantSessionTokens.Add(sessionToken);

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                returnedToken = string.Empty;
                await transaction.CommitAsync(cancellationToken);
            }

            return Result.Success(new JoinGameResponse(
                existingParticipant.Id,
                returnedToken,
                game.Id,
                existingParticipant.DisplayNickname,
                game.Title,
                existingParticipant.SeatNumber));
        }

        bool nicknameTaken = await _dbContext.Participants
            .AnyAsync(
                p => p.GameId == game.Id && p.NormalizedNickname == normalizedNickname,
                cancellationToken);

        if (nicknameTaken)
        {
            return Result.Failure<JoinGameResponse>(GameErrors.NicknameTaken);
        }

        int activeSeats = await _dbContext.Participants
            .CountAsync(
                p => p.GameId == game.Id && !p.IsRemoved,
                cancellationToken);

        if (activeSeats >= 500)
        {
            return Result.Failure<JoinGameResponse>(GameErrors.Full);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        int seatNumber = game.NextSeatNumber;
        game.NextSeatNumber += 1;
        game.ReservedParticipantCount += 1;
        game.PresenceVersion += 1;

        Guid participantId = Guid.NewGuid();
        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        string rawToken = "pst_" + Convert.ToHexString(tokenBytes).ToLowerInvariant();
        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

        Participant participant = new Participant
        {
            Id = participantId,
            HostAccountId = game.HostAccountId,
            GameId = game.Id,
            DisplayNickname = displayNickname,
            NormalizedNickname = normalizedNickname,
            SeatNumber = seatNumber,
            JoinOperationIdHash = joinOperationIdHash,
            JoinRecoveryExpiresAt = utcNow.AddMinutes(15),
            IsRemoved = false,
            RemovedAt = null,
            TotalScore = 0,
            Rank = null,
            ConnectionGeneration = 1,
            CreatedAt = utcNow
        };

        ParticipantSessionToken newSessionToken = new ParticipantSessionToken
        {
            Id = Guid.NewGuid(),
            HostAccountId = game.HostAccountId,
            GameId = game.Id,
            ParticipantId = participantId,
            TokenHash = tokenHash,
            CreatedAt = utcNow,
            RevokedAt = null,
            ExpiresAt = null
        };

        _dbContext.Participants.Add(participant);
        _dbContext.ParticipantSessionTokens.Add(newSessionToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (
            exception.ConstraintName == "ux_participants_join_operation")
        {
            return Result.Failure<JoinGameResponse>(Error.Validation(
                "Validation.Failed", "Mismatched join operation parameters."));
        }
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Player joined game. EventName={EventName} GameId={GameId} ParticipantId={ParticipantId} SeatNumber={SeatNumber}",
            "PlayerJoined",
            game.Id,
            participantId,
            seatNumber);

        int connectedCount = await _playerPresenceService.GetConnectedCountAsync(game.Id);
        ParticipantPresenceChangedEvent presenceEvent = new ParticipantPresenceChangedEvent(
            game.Id,
            game.PresenceVersion,
            game.ReservedParticipantCount,
            connectedCount,
            participant.DisplayNickname,
            participant.SeatNumber,
            "Joined");

        await _notificationService.PublishParticipantPresenceChangedAsync(
            game.HostAccountId,
            game.Id,
            game.PresenceVersion,
            presenceEvent,
            CancellationToken.None);

        JoinGameResponse response = new JoinGameResponse(
            participant.Id,
            rawToken,
            game.Id,
            participant.DisplayNickname,
            game.Title,
            participant.SeatNumber);

        return Result.Success(response);
    }
}
