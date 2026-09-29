namespace Kahoot.Application.Features.Games;

using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public static class QuestionResultsMaterializer
{
    // Question Results Aggregation (SCORE-RES-001) - Computes accepted submission counts and per-choice selections directly in PostgreSQL
    public static async Task<(GameQuestionSnapshot Question, List<GameChoiceSnapshot> Choices)> MaterializeAsync(
        IAppDbContext dbContext,
        Game game,
        DateTimeOffset closedAt,
        CancellationToken cancellationToken)
    {
        if (game.CurrentQuestionIndex is not int currentQuestionIndex)
        {
            throw new InvalidOperationException("An active game has no current question index.");
        }

        // Query Performance - SingleOrDefaultAsync seeks active question snapshot on composite unique index (GameId, HostAccountId, OrderIndex)
        GameQuestionSnapshot question = await dbContext.GameQuestionSnapshots
            .SingleOrDefaultAsync(
                snapshot => snapshot.GameId == game.Id &&
                            snapshot.HostAccountId == game.HostAccountId &&
                            snapshot.OrderIndex == currentQuestionIndex,
                cancellationToken)
            ?? throw new InvalidOperationException("An active game has no current question snapshot.");

        question.EndsAt = question.EndsAt.HasValue && question.EndsAt.Value < closedAt
            ? question.EndsAt.Value
            : closedAt;
        question.ResultsMaterializedAt = closedAt;

        // Query Performance - Fetches ordered choices for snapshot to track selection counts
        List<GameChoiceSnapshot> choices = await dbContext.GameChoiceSnapshots
            .Where(choice => choice.GameQuestionId == question.Id && choice.HostAccountId == game.HostAccountId)
            .OrderBy(choice => choice.OrderIndex)
            .ToListAsync(cancellationToken);

        // Server-Side Aggregation (SCORE-RES-001) - GroupBy and Count() aggregate selection counts directly in PostgreSQL, eliminating O(N) submission transfers
        Dictionary<Guid, int> selectionCounts = await dbContext.AnswerSubmissionChoices
            .Where(selection => selection.GameQuestionId == question.Id && selection.HostAccountId == game.HostAccountId)
            .GroupBy(selection => selection.GameChoiceId)
            .Select(group => new { ChoiceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ChoiceId, item => item.Count, cancellationToken);

        foreach (GameChoiceSnapshot choice in choices)
        {
            choice.SelectionCount = selectionCounts.GetValueOrDefault(choice.Id);
        }

        // Query Performance - CountAsync counts accepted submissions directly in database engine
        question.AcceptedAnswerCount = await dbContext.AnswerSubmissions
            .CountAsync(
                submission => submission.GameId == game.Id &&
                              submission.GameQuestionId == question.Id &&
                              submission.HostAccountId == game.HostAccountId,
                cancellationToken);

        return (question, choices);
    }

    // Scorecard Generation (SCORE-RES-002) - Materializes individual scorecard projection for each non-removed participant in the game session
    public static async Task<List<PersonalQuestionResultEvent>> MaterializePersonalResultsAsync(
        IAppDbContext dbContext,
        Game game,
        GameQuestionSnapshot question,
        long stateVersion,
        CancellationToken cancellationToken)
    {
        // Query Performance - AsNoTracking() and Left Join materialize individual player scores without change-tracker allocation
        return await (
            from participant in dbContext.Participants.AsNoTracking()
            where participant.GameId == game.Id &&
                  participant.HostAccountId == game.HostAccountId &&
                  !participant.IsRemoved
            join submission in dbContext.AnswerSubmissions.AsNoTracking()
                on new { participant.GameId, QuestionId = question.Id, ParticipantId = participant.Id }
                equals new { submission.GameId, QuestionId = submission.GameQuestionId, submission.ParticipantId } into submissions
            from sub in submissions.DefaultIfEmpty()
            select new PersonalQuestionResultEvent(
                participant.Id,
                game.Id,
                stateVersion,
                question.Id,
                question.OrderIndex,
                sub != null,
                sub != null && sub.IsCorrect,
                sub != null ? sub.PointsAwarded : 0,
                participant.TotalScore)
        ).ToListAsync(cancellationToken);
    }
}
