using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.GetHostGameState;

internal sealed class GetHostGameStateQueryHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetHostGameStateQuery, HostGameStateResponse>
{
    public async Task<Result<HostGameStateResponse>> Handle(
        GetHostGameStateQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<HostGameStateResponse>(SharedErrors.Unauthorized);
        }

        var game = await dbContext.GameSessions
            .AsNoTracking()
            .Where(session => session.Id == query.GameId && session.HostId == hostId)
            .Select(session => new
            {
                session.Id,
                session.Pin,
                QuizTitle = session.Quiz!.Title,
                session.Status,
                session.CurrentQuestionId,
                session.CurrentQuestionIndex,
                session.CurrentQuestionStartedAt,
                session.CurrentQuestionEndsAt,
                TotalQuestions = session.Quiz.Questions.Count
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null)
        {
            return Result.Failure<HostGameStateResponse>(GameErrors.NotFound);
        }

        List<GameParticipantResponse> participants = await dbContext.Participants
            .AsNoTracking()
            .Where(participant => participant.GameSessionId == game.Id)
            .OrderByDescending(participant => participant.TotalScore)
            .ThenBy(participant => participant.Nickname)
            .Select(participant => new GameParticipantResponse(
                participant.Id,
                participant.Nickname,
                participant.TotalScore,
                participant.LastRank,
                participant.ConnectionId != null,
                participant.IsRemoved))
            .ToListAsync(cancellationToken);

        int answeredCount = game.CurrentQuestionId is { } questionId
            ? await dbContext.Answers.CountAsync(
                answer => answer.GameSessionId == game.Id && answer.QuestionId == questionId,
                cancellationToken)
            : 0;

        return Result.Success(new HostGameStateResponse(
            game.Id,
            game.Pin,
            game.QuizTitle,
            game.Status,
            game.CurrentQuestionIndex,
            game.TotalQuestions,
            game.CurrentQuestionStartedAt is { } startedAt ? startedAt.ToUtcOffset() : null,
            game.CurrentQuestionEndsAt is { } endsAt ? endsAt.ToUtcOffset() : null,
            answeredCount,
            participants));
    }
}
