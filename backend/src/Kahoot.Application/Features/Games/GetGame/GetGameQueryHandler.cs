namespace Kahoot.Application.Features.Games.GetGame;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public sealed class GetGameQueryHandler : IQueryHandler<GetGameQuery, GetGameResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetGameQueryHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<GetGameResponse>> Handle(
        GetGameQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<GetGameResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Query Performance: AsNoTracking() skips tracker overhead for read-only game metadata projection
        Game? game = await _dbContext.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.Id == request.GameId && g.HostAccountId == hostAccountId,
                cancellationToken);

        if (game is null)
        {
            return Result.Failure<GetGameResponse>(GameErrors.NotFound);
        }

        // Query Performance: CountAsync calculates total questions directly in SQL engine
        int totalQuestions = await _dbContext.GameQuestionSnapshots
            .CountAsync(
                q => q.GameId == game.Id && q.HostAccountId == hostAccountId,
                cancellationToken);

        // Query Performance: CountAsync computes active participant count directly in SQL engine
        int participantCount = await _dbContext.Participants
            .CountAsync(
                p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved,
                cancellationToken);

        GetGameResponse response = new GetGameResponse(
            game.Id,
            game.Pin,
            game.Title,
            game.Status switch
            {
                GameStatus.Created => "CREATED",
                GameStatus.Lobby => "LOBBY",
                GameStatus.QuestionActive => "QUESTION_ACTIVE",
                GameStatus.QuestionResults => "QUESTION_RESULTS",
                GameStatus.Leaderboard => "LEADERBOARD",
                GameStatus.Finished => "FINISHED",
                _ => throw new InvalidOperationException("Unknown game status.")
            },
            game.StateVersion,
            game.CurrentQuestionIndex,
            totalQuestions,
            participantCount,
            game.HostGraceExpiresAt,
            game.CreatedAt,
            game.FinishedAt);

        return Result.Success(response);
    }
}
