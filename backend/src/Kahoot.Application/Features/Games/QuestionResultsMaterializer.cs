namespace Kahoot.Application.Features.Games;

using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public static class QuestionResultsMaterializer
{
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

        List<GameChoiceSnapshot> choices = await dbContext.GameChoiceSnapshots
            .Where(choice => choice.GameQuestionId == question.Id && choice.HostAccountId == game.HostAccountId)
            .OrderBy(choice => choice.OrderIndex)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, int> selectionCounts = await dbContext.AnswerSubmissionChoices
            .Where(selection => selection.GameQuestionId == question.Id && selection.HostAccountId == game.HostAccountId)
            .GroupBy(selection => selection.GameChoiceId)
            .Select(group => new { ChoiceId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ChoiceId, item => item.Count, cancellationToken);

        foreach (GameChoiceSnapshot choice in choices)
        {
            choice.SelectionCount = selectionCounts.GetValueOrDefault(choice.Id);
        }

        question.AcceptedAnswerCount = await dbContext.AnswerSubmissions
            .CountAsync(
                submission => submission.GameId == game.Id &&
                              submission.GameQuestionId == question.Id &&
                              submission.HostAccountId == game.HostAccountId,
                cancellationToken);

        return (question, choices);
    }
}
