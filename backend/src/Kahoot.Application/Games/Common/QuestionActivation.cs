using Kahoot.Domain.Games;
using Kahoot.Domain.Quizzes;

namespace Kahoot.Application.Games.Common;

internal static class QuestionActivation
{
    public static QuestionStartedResponse Activate(
        GameSession game,
        IReadOnlyList<Question> orderedQuestions,
        int index,
        DateTimeOffset now)
    {
        Question question = orderedQuestions[index];
        DateTimeOffset endsAt = now.AddSeconds(question.TimeLimitSeconds);

        game.Status = GameStatus.QuestionActive;
        game.CurrentQuestionId = question.Id;
        game.CurrentQuestionIndex = index;
        game.CurrentQuestionStartedAt = now.UtcDateTime;
        game.CurrentQuestionEndsAt = endsAt.UtcDateTime;
        game.StartedAt ??= now.UtcDateTime;

        return GameQuestionMapper.BuildStarted(question, index, orderedQuestions.Count, now, endsAt);
    }

    public static QuestionStartedResponse Rebuild(GameSession game, IReadOnlyList<Question> orderedQuestions)
    {
        int index = game.CurrentQuestionIndex ?? 0;
        Question question = orderedQuestions[index];

        return GameQuestionMapper.BuildStarted(
            question,
            index,
            orderedQuestions.Count,
            game.CurrentQuestionStartedAt!.Value.ToUtcOffset(),
            game.CurrentQuestionEndsAt!.Value.ToUtcOffset());
    }
}
