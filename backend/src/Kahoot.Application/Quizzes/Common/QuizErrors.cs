using Kahoot.Domain.Common;

namespace Kahoot.Application.Quizzes.Common;

public static class QuizErrors
{
    public static readonly Error NotFound =
        new("Quiz.NotFound", "The quiz was not found.");

    public static readonly Error QuestionNotFound =
        new("Quiz.QuestionNotFound", "The question was not found in this quiz.");

    public static readonly Error InUse =
        new("Quiz.InUse", "The quiz cannot be modified while it has a game session that is not finished.");

    public static readonly Error HasSessions =
        new("Quiz.HasSessions", "The quiz cannot be deleted because it has been used to run a game.");

    public static readonly Error NotPublishable =
        new("Quiz.NotPublishable", "The quiz needs at least one question, and every question needs 2-6 choices with exactly one correct answer.");

    public static readonly Error ConcurrentModification =
        new("Quiz.ConcurrentModification", "The quiz was modified concurrently. Please retry.");

    public static readonly Error QuestionSetMismatch =
        new("Quiz.QuestionSetMismatch", "The provided question list does not match this quiz's questions.");
}
