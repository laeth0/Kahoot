using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Quizzes;

public static class QuizErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Quiz.NotFound",
        "The requested quiz was not found.");

    public static readonly Error QuestionNotFound = Error.NotFound(
        "Quiz.QuestionNotFound",
        "The requested question was not found in this quiz.");

    public static readonly Error InUse = Error.Conflict(
        "Quiz.InUse",
        "The quiz cannot be modified or deleted because an active game session is currently in progress.");

    public static readonly Error HasSessions = Error.Conflict(
        "Quiz.HasSessions",
        "The quiz cannot be deleted because it has previously been used in a game session.");

    public static readonly Error ConcurrentModification = Error.Conflict(
        "Quiz.ConcurrentModification",
        "The quiz was modified by another operation. Please refresh and try again.");

    public static readonly Error QuestionSetMismatch = Error.Validation(
        "Quiz.QuestionSetMismatch",
        "The question IDs provided for reordering do not match the quiz's current questions.");

    public static readonly Error InvalidMediaReference = Error.Validation(
        "Quiz.InvalidMediaReference",
        "The referenced media item does not exist or belongs to another account.");

    public static readonly Error QuestionLimitExceeded = Error.Validation(
        "Validation.Failed",
        "A quiz cannot contain more than 200 questions.");
}
