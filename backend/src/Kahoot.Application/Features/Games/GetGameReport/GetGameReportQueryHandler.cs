namespace Kahoot.Application.Features.Games.GetGameReport;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public sealed class GetGameReportQueryHandler : IQueryHandler<GetGameReportQuery, GetGameReportResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetGameReportQueryHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<GetGameReportResponse>> Handle(
        GetGameReportQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<GetGameReportResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Query Performance: AsNoTracking() retrieves finished game without tracker overhead
        Game? game = await _dbContext.Games
            .AsNoTracking()
            .FirstOrDefaultAsync(
                g => g.Id == request.GameId && g.HostAccountId == hostAccountId,
                cancellationToken);

        if (game is null)
        {
            return Result.Failure<GetGameReportResponse>(GameErrors.NotFound);
        }

        if (game.Status != GameStatus.Finished)
        {
            return Result.Failure<GetGameReportResponse>(GameErrors.InvalidStateTransition);
        }

        // Query Performance: AsNoTracking() loads question snapshots ordered by sequence
        List<GameQuestionSnapshot> questions = await _dbContext.GameQuestionSnapshots
            .AsNoTracking()
            .Where(q => q.GameId == game.Id && q.HostAccountId == hostAccountId)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(cancellationToken);

        List<Guid> questionIds = questions.Select(q => q.Id).ToList();

        // Query Performance: AsNoTracking() and Contains() batch-load choices for all questions in one round trip
        List<GameChoiceSnapshot> choices = await _dbContext.GameChoiceSnapshots
            .AsNoTracking()
            .Where(c => questionIds.Contains(c.GameQuestionId) && c.HostAccountId == hostAccountId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        // System Design & Server-Side Aggregation: GroupBy and Count() compute correct answer totals in database without loading submission rows
        Dictionary<Guid, int> correctAnswersCount = await _dbContext.AnswerSubmissions
            .AsNoTracking()
            .Where(sub => sub.GameId == game.Id && sub.HostAccountId == hostAccountId && sub.IsCorrect)
            .GroupBy(sub => sub.GameQuestionId)
            .Select(group => new { QuestionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.QuestionId, item => item.Count, cancellationToken);

        Dictionary<Guid, List<GameChoiceSnapshot>> choicesByQuestionId = choices
            .GroupBy(c => c.GameQuestionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        List<QuestionReportDto> questionReports = questions
            .Select(q =>
            {
                List<QuestionChoiceResultDto> choiceDtos = choicesByQuestionId.TryGetValue(q.Id, out List<GameChoiceSnapshot>? qChoices)
                    ? qChoices.Select(c => new QuestionChoiceResultDto(c.Id, c.OrderIndex, c.Text, c.IsCorrect, c.SelectionCount)).ToList()
                    : new List<QuestionChoiceResultDto>();

                int correctCount = correctAnswersCount.TryGetValue(q.Id, out int cCount) ? cCount : 0;

                return new QuestionReportDto(
                    q.OrderIndex,
                    q.Text,
                    q.AcceptedAnswerCount,
                    correctCount,
                    choiceDtos);
            })
            .ToList();

        // Query Performance - AsNoTracking() loads ranked participants for report leaderboard with deterministic tie-breaking (SCORE-RANK-001)
        List<Participant> participants = await _dbContext.Participants
            .AsNoTracking()
            .Where(p => p.GameId == game.Id && p.HostAccountId == hostAccountId && !p.IsRemoved)
            .OrderBy(p => p.Rank ?? int.MaxValue)
            .ThenByDescending(p => p.TotalScore)
            .ThenBy(p => p.NormalizedNickname)
            .ThenBy(p => p.Id)
            .ToListAsync(cancellationToken);

        List<LeaderboardParticipantDto> leaderboard = participants
            .Select(p => new LeaderboardParticipantDto(p.Id, p.DisplayNickname, p.TotalScore, p.Rank ?? 0))
            .ToList();

        GetGameReportResponse response = new GetGameReportResponse(
            game.Id,
            game.Title,
            "FINISHED",
            questions.Count,
            participants.Count,
            game.CreatedAt,
            game.FinishedAt,
            questionReports,
            leaderboard);

        return Result.Success(response);
    }
}
