using Kahoot.Domain.Games;

namespace Kahoot.Application.Games.Common;

internal static class QuestionActivation
{
    public static QuestionStartedResponse Activate(
        GameSession game,
        GameQuestionSnapshot question,
        int index,
        int totalQuestions,
        int eligibleCount,
        DateTimeOffset now)
    {
        DateTimeOffset endsAt = now.AddSeconds(question.TimeLimitSeconds);

        game.Status = GameStatus.QuestionActive;
        game.CurrentQuestionId = question.Id;
        game.CurrentQuestionIndex = index;
        game.CurrentQuestionStartedAt = now.UtcDateTime;
        game.CurrentQuestionEndsAt = endsAt.UtcDateTime;
        game.CurrentQuestionEligibleCount = eligibleCount;
        game.CurrentQuestionAnsweredCount = 0;
        game.StartedAt ??= now.UtcDateTime;

        return GameQuestionMapper.BuildStarted(question, index, totalQuestions, now, endsAt);
    }

    public static QuestionStartedResponse Rebuild(GameSession game, GameQuestionSnapshot question, int totalQuestions) =>
        GameQuestionMapper.BuildStarted(
            question,
            game.CurrentQuestionIndex ?? 0,
            totalQuestions,
            game.CurrentQuestionStartedAt!.Value.ToUtcOffset(),
            game.CurrentQuestionEndsAt!.Value.ToUtcOffset());
}
