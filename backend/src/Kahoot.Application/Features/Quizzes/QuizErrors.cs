using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Quizzes;

public static class QuizErrors
{
    // Tenant Isolation Boundary - Emitted when quiz does not exist or belongs to another Host tenant (QUIZ-ERR-004)
    public static readonly Error NotFound = Error.NotFound(
        "Quiz.NotFound",
        "The requested quiz was not found.");

    // Resource Ownership Invariant - Emitted when target question does not exist within the specified quiz (QUIZ-ERR-005)
    public static readonly Error QuestionNotFound = Error.NotFound(
        "Quiz.QuestionNotFound",
        "The requested question was not found in this quiz.");

    // Active Session Mutual Exclusion - Prevents editing or deleting a quiz while a live game session is running (QUIZ-ERR-006, QUIZ-RISK-001)
    public static readonly Error InUse = Error.Conflict(
        "Quiz.InUse",
        "The quiz cannot be modified or deleted because an active game session is currently in progress.");

    // Historical Audit Retention - Enforces ever-played invariant preventing deletion of quizzes used in any game (QUIZ-DEL-001, QUIZ-ERR-007)
    public static readonly Error HasSessions = Error.Conflict(
        "Quiz.HasSessions",
        "The quiz cannot be deleted because it has previously been used in a game session.");

    // Optimistic Concurrency Control (OCC) - Detects concurrent edits or stale revisions on quiz authoring (QUIZ-ERR-008, QUIZ-RISK-002)
    public static readonly Error ConcurrentModification = Error.Conflict(
        "Quiz.ConcurrentModification",
        "The quiz was modified by another operation. Please refresh and try again.");

    // Permutation Integrity Invariant - Rejects reorder requests that do not strictly form a 1-to-1 bijection with existing questions (QUIZ-REORDER-001, QUIZ-ERR-002)
    public static readonly Error QuestionSetMismatch = Error.Validation(
        "Quiz.QuestionSetMismatch",
        "The question IDs provided for reordering do not match the quiz's current questions.");

    // Cross-Tenant Image Isolation - Rejects images that belong to another tenant or are already attached to another question (QUIZ-SEC-001, QUIZ-ERR-003)
    public static readonly Error InvalidImageReference = Error.Validation(
        "Quiz.InvalidImageReference",
        "The referenced image is unavailable, belongs to another account, or is attached to another question.");

    // Technical System Safety Bound - Enforces 200-question ceiling to guarantee bounded memory and snapshot SLOs (QUIZ-LIMIT-001, QUIZ-BOUND-003)
    public static readonly Error QuestionLimitExceeded = Error.Validation(
        "Validation.Failed",
        "A quiz cannot contain more than 200 questions.");
}
