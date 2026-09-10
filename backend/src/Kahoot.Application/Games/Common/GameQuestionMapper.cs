using Kahoot.Domain.Quizzes;

namespace Kahoot.Application.Games.Common;

internal static class GameQuestionMapper
{
    public static QuestionStartedResponse BuildStarted(
        Question question,
        int questionIndex,
        int totalQuestions,
        DateTimeOffset startedAt,
        DateTimeOffset endsAt)
    {
        Choice[] ordered = [.. question.Choices.OrderBy(choice => choice.OrderIndex)];
        IReadOnlyList<Guid> correctChoiceIds = [.. ordered.Where(choice => choice.IsCorrect).Select(choice => choice.Id)];

        PlayerQuestionResponse player = new(
            question.Id,
            questionIndex,
            totalQuestions,
            question.Text,
            question.ImageUrl,
            question.TimeLimitSeconds,
            endsAt,
            [.. ordered.Select(choice => new PlayerChoiceResponse(choice.Id, choice.OrderIndex, choice.Text, choice.ImageUrl))]);

        HostQuestionResponse host = new(
            question.Id,
            questionIndex,
            totalQuestions,
            question.Text,
            question.ImageUrl,
            question.TimeLimitSeconds,
            startedAt,
            endsAt,
            correctChoiceIds,
            [.. ordered.Select(choice => new HostChoiceResponse(choice.Id, choice.OrderIndex, choice.Text, choice.ImageUrl, choice.IsCorrect))]);

        return new QuestionStartedResponse(host, player);
    }

    public static PlayerQuestionResponse BuildPlayerView(
        Question question,
        int questionIndex,
        int totalQuestions,
        DateTimeOffset endsAt) =>
        new(
            question.Id,
            questionIndex,
            totalQuestions,
            question.Text,
            question.ImageUrl,
            question.TimeLimitSeconds,
            endsAt,
            [.. question.Choices
                .OrderBy(choice => choice.OrderIndex)
                .Select(choice => new PlayerChoiceResponse(choice.Id, choice.OrderIndex, choice.Text, choice.ImageUrl))]);
}
