namespace Kahoot.Application.Features.Games.EndGame;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

public sealed class EndGameCommandHandler : ICommandHandler<EndGameCommand, EndGameResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IGameCommandIdempotencyService _idempotencyService;
    private readonly IGameNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EndGameCommandHandler> _logger;

    public EndGameCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IGameCommandIdempotencyService idempotencyService,
        IGameNotificationService notificationService,
        TimeProvider timeProvider,
        ILogger<EndGameCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _idempotencyService = idempotencyService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<EndGameResponse>> Handle(
        EndGameCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<EndGameResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<EndGameResponse>(AuthErrors.Unauthorized);
        }

        Game? game = await _dbContext.GetGameForUpdateAsync(request.GameId, hostAccountId, cancellationToken);
        if (game is null)
        {
            return Result.Failure<EndGameResponse>(GameErrors.NotFound);
        }

        IdempotencyCheckResult<EndGameResponse> idempotencyResult = await _idempotencyService.CheckAsync<EndGameResponse>(
            request.GameId,
            request.CommandId,
            nameof(EndGameCommand),
            request,
            cancellationToken);

        if (idempotencyResult.IsReplay)
        {
            if (idempotencyResult.Error is not null)
            {
                return Result.Failure<EndGameResponse>(idempotencyResult.Error);
            }

            return Result.Success(idempotencyResult.CachedResponse!);
        }

        if (game.Status == GameStatus.Finished)
        {
            return Result.Failure<EndGameResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Result.Failure<EndGameResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.StateVersion != request.ExpectedStateVersion)
        {
            return Result.Failure<EndGameResponse>(GameErrors.ConcurrentModification);
        }

        if (game.Status == GameStatus.Created)
        {
            return Result.Failure<EndGameResponse>(GameErrors.InvalidStateTransition);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        if (game.Status == GameStatus.QuestionActive)
        {
            await QuestionResultsMaterializer.MaterializeAsync(_dbContext, game, utcNow, cancellationToken);
        }

        game.Status = GameStatus.Finished;
        game.FinishedAt = utcNow;
        game.HostGraceExpiresAt = null;
        game.Pin = null;
        game.StateVersion += 1;

        await ParticipantRankMaterializer.MaterializeAsync(
            _dbContext, game.Id, hostAccountId, cancellationToken);

        int activeParticipantCount = await _dbContext.Participants
            .CountAsync(p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved,
                cancellationToken);

        List<PodiumParticipantDto> podium = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved)
            .OrderBy(p => p.Rank)
            .Take(3)
            .Select(p => new PodiumParticipantDto(p.Rank!.Value, p.DisplayNickname, p.TotalScore))
            .ToListAsync(cancellationToken);

        EndGameResponse response = new EndGameResponse(
            game.Id,
            "FINISHED",
            game.StateVersion,
            utcNow,
            podium);

        await _idempotencyService.RecordAsync(
            game.Id,
            hostAccountId,
            request.CommandId,
            nameof(EndGameCommand),
            request,
            game.StateVersion,
            response,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Game ended. EventName={EventName} GameId={GameId} StateVersion={StateVersion} FinishedAt={FinishedAt} TotalParticipants={TotalParticipants}",
            "GameEnded",
            game.Id,
            game.StateVersion,
            utcNow,
            activeParticipantCount);

        await _notificationService.PublishGameEndedAsync(
            hostAccountId,
            game.Id,
            game.StateVersion,
            response,
            CancellationToken.None);

        return Result.Success(response);
    }
}
