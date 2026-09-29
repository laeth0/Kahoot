namespace Kahoot.Application.Features.Games.ShowLeaderboard;

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

public sealed class ShowLeaderboardCommandHandler : ICommandHandler<ShowLeaderboardCommand, ShowLeaderboardResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IGameCommandIdempotencyService _idempotencyService;
    private readonly IGameNotificationService _notificationService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ShowLeaderboardCommandHandler> _logger;

    public ShowLeaderboardCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IGameCommandIdempotencyService idempotencyService,
        IGameNotificationService notificationService,
        TimeProvider timeProvider,
        ILogger<ShowLeaderboardCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _idempotencyService = idempotencyService;
        _notificationService = notificationService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<ShowLeaderboardResponse>> Handle(
        ShowLeaderboardCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<ShowLeaderboardResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<ShowLeaderboardResponse>(AuthErrors.Unauthorized);
        }

        Game? game = await _dbContext.GetGameForUpdateAsync(request.GameId, hostAccountId, cancellationToken);
        if (game is null)
        {
            return Result.Failure<ShowLeaderboardResponse>(GameErrors.NotFound);
        }

        IdempotencyCheckResult<ShowLeaderboardResponse> idempotencyResult = await _idempotencyService.CheckAsync<ShowLeaderboardResponse>(
            request.GameId,
            request.CommandId,
            nameof(ShowLeaderboardCommand),
            request,
            cancellationToken);

        if (idempotencyResult.IsReplay)
        {
            if (idempotencyResult.Error is not null)
            {
                return Result.Failure<ShowLeaderboardResponse>(idempotencyResult.Error);
            }

            return Result.Success(idempotencyResult.CachedResponse!);
        }

        if (game.Status == GameStatus.Finished)
        {
            return Result.Failure<ShowLeaderboardResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.HostGraceExpiresAt <= _timeProvider.GetUtcNow())
        {
            return Result.Failure<ShowLeaderboardResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.StateVersion != request.ExpectedStateVersion)
        {
            return Result.Failure<ShowLeaderboardResponse>(GameErrors.ConcurrentModification);
        }

        if (game.Status != GameStatus.QuestionResults)
        {
            return Result.Failure<ShowLeaderboardResponse>(GameErrors.InvalidStateTransition);
        }

        await ParticipantRankMaterializer.MaterializeAsync(
            _dbContext, game.Id, hostAccountId, cancellationToken);

        int activeParticipantCount = await _dbContext.Participants
            .CountAsync(p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved,
                cancellationToken);

        List<LeaderboardParticipantDto> topParticipants = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved)
            .OrderBy(p => p.Rank)
            .Take(5)
            .Select(p => new LeaderboardParticipantDto(p.Id, p.DisplayNickname, p.TotalScore, p.Rank!.Value))
            .ToListAsync(cancellationToken);

        game.Status = GameStatus.Leaderboard;
        game.StateVersion += 1;

        ShowLeaderboardResponse response = new ShowLeaderboardResponse(
            game.Id,
            "LEADERBOARD",
            game.StateVersion,
            topParticipants);

        await _idempotencyService.RecordAsync(
            game.Id,
            hostAccountId,
            request.CommandId,
            nameof(ShowLeaderboardCommand),
            request,
            game.StateVersion,
            response,
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Leaderboard displayed. EventName={EventName} GameId={GameId} StateVersion={StateVersion} ParticipantCount={ParticipantCount}",
            "LeaderboardDisplayed",
            game.Id,
            game.StateVersion,
            activeParticipantCount);

        await _notificationService.PublishLeaderboardUpdatedAsync(
            hostAccountId,
            game.Id,
            game.StateVersion,
            response,
            CancellationToken.None);

        return Result.Success(response);
    }
}
