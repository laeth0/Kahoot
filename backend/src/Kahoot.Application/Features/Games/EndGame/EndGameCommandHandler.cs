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

        // Transactional Atomicity: Wraps game termination, final podium ranking, and PIN release in a single transaction
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Concurrency & Pessimistic Row Lock: GetUserForUpdateAsync and GetGameForUpdateAsync serialize game termination
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

        // System Design & Command Idempotency: Ensures retried EndGame requests safely return cached podium response
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

        // Optimistic Concurrency Control (OCC): Prevents conflicting terminal state changes
        if (game.StateVersion != request.ExpectedStateVersion)
        {
            return Result.Failure<EndGameResponse>(GameErrors.ConcurrentModification);
        }

        if (game.Status == GameStatus.Created)
        {
            return Result.Failure<EndGameResponse>(GameErrors.InvalidStateTransition);
        }

        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        // System Design & Final Question Materialization: If terminating during active question, materializes remaining results
        if (game.Status == GameStatus.QuestionActive)
        {
            await QuestionResultsMaterializer.MaterializeAsync(_dbContext, game, utcNow, cancellationToken);
        }

        game.Status = GameStatus.Finished;
        game.FinishedAt = utcNow;
        game.HostGraceExpiresAt = null;

        // System Design & PIN Reclamation: Setting Pin = null releases the 6-digit PIN back into the active pool for new games
        game.Pin = null;
        game.StateVersion += 1;

        // System Design & Final Rank Materialization: Computes definitive ranks for podium and archival reporting
        await ParticipantRankMaterializer.MaterializeAsync(
            _dbContext, game.Id, hostAccountId, cancellationToken);

        // Post-Game Expiry Window (RECON-WINDOW-001) - Enforces 24-hour read-only recovery boundary on participant session tokens via ExecuteUpdateAsync
        await _dbContext.ParticipantSessionTokens
            .Where(token => token.GameId == game.Id && token.HostAccountId == hostAccountId && token.ExpiresAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(token => token.ExpiresAt, utcNow.AddHours(24)), cancellationToken);


        int activeParticipantCount = await _dbContext.Participants
            .CountAsync(p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved,
                cancellationToken);

        // Query Performance & Podium Bounding: AsNoTracking() and Take(3) retrieve strictly top-3 finalists for victory ceremony
        List<PodiumParticipantDto> podium = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved)
            .OrderBy(p => p.Rank)
            .Take(3)
            .Select(p => new PodiumParticipantDto(p.Rank!.Value, p.DisplayNickname, p.TotalScore))
            .ToListAsync(cancellationToken);

        // Personal Final Standing Materialization (SCORE-RANK-001) - Projects final rank position for each active participant to push scorecard
        List<PersonalGameEndedEvent> personalRanks = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved)
            .Select(p => new PersonalGameEndedEvent(p.Id, game.Id, game.StateVersion, p.Rank!.Value, p.TotalScore))
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

        // Persistence: Commits terminal game status, released PIN, and podium data
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Game ended. EventName={EventName} GameId={GameId} StateVersion={StateVersion} FinishedAt={FinishedAt} TotalParticipants={TotalParticipants}",
            "GameEnded",
            game.Id,
            game.StateVersion,
            utcNow,
            activeParticipantCount);

        // Post-Commit Broadcast Pattern - Broadcasts game finished event to all connected sockets and individual final ranks post-commit
        await _notificationService.PublishGameEndedWithPersonalRanksAsync(
            hostAccountId,
            game.Id,
            game.StateVersion,
            response,
            personalRanks,
            CancellationToken.None);

        return Result.Success(response);
    }
}
