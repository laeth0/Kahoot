using Kahoot.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.Common;

internal static class QuestionResultsBuilder
{
    public static async Task<QuestionResultsResponse?> BuildAsync(
        IApplicationDbContext dbContext,
        Guid gameId,
        Guid questionId,
        int questionIndex,
        CancellationToken cancellationToken)
    {
        var choices = await dbContext.Choices
            .AsNoTracking()
            .Where(choice => choice.QuestionId == questionId)
            .OrderBy(choice => choice.OrderIndex)
            .Select(choice => new { choice.Id, choice.Text, choice.IsCorrect })
            .ToListAsync(cancellationToken);

        if (choices.Count == 0 || choices.All(choice => !choice.IsCorrect))
        {
            return null;
        }

        var counts = await dbContext.Answers
            .AsNoTracking()
            .Where(answer => answer.GameSessionId == gameId && answer.QuestionId == questionId)
            .GroupBy(answer => answer.SelectedChoiceId)
            .Select(group => new { ChoiceId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, int> countByChoice = counts.ToDictionary(entry => entry.ChoiceId, entry => entry.Count);

        int participantCount = await dbContext.Participants
            .AsNoTracking()
            .CountAsync(participant => participant.GameSessionId == gameId && !participant.IsRemoved, cancellationToken);

        List<ChoiceResultResponse> choiceResults =
        [
            .. choices.Select(choice => new ChoiceResultResponse(
                choice.Id,
                choice.Text,
                countByChoice.GetValueOrDefault(choice.Id),
                choice.IsCorrect))
        ];

        return new QuestionResultsResponse(
            questionId,
            questionIndex,
            choices.First(choice => choice.IsCorrect).Id,
            participantCount,
            countByChoice.Values.Sum(),
            choiceResults);
    }
}
