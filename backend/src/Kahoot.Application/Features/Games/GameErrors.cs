using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Games;

public static class GameErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Game.NotFound",
        "The requested game session was not found.");

    public static readonly Error QuizNotPublished = Error.Conflict(
        "Game.QuizNotPublished",
        "Cannot create a game session from an unpublished draft quiz.");

    public static readonly Error InvalidStateTransition = Error.Conflict(
        "Game.InvalidStateTransition",
        "The requested state transition is not permitted from the current game state.");

    public static readonly Error NoMoreQuestions = Error.Conflict(
        "Game.NoMoreQuestions",
        "There are no more questions remaining in the quiz.");

    public static readonly Error ConcurrentModification = Error.Conflict(
        "Game.ConcurrentModification",
        "The game session was modified by another operation. Please refresh and try again.");

    public static readonly Error PinUnavailable = Error.Conflict(
        "Game.PinUnavailable",
        "Failed to generate a unique PIN for the game session after maximum retry attempts.");

    public static readonly Error AnswerTooLate = Error.Conflict(
        "Game.AnswerTooLate",
        "The answer was submitted after the question deadline expired.");

    public static readonly Error ArchiveImmutable = Error.Conflict(
        "Game.ArchiveImmutable",
        "The game session is finished and its historical archive cannot be modified.");

    public static readonly Error Full = Error.Conflict(
        "Game.Full",
        "The game session has reached its maximum participant capacity.");

    public static readonly Error InvalidPin = Error.NotFound(
        "Game.InvalidPin",
        "The provided PIN does not match any active game session.");

    public static readonly Error ParticipantNotFound = Error.NotFound(
        "Game.ParticipantNotFound",
        "The participant was not found in this game session.");

    public static readonly Error NotJoinable = Error.Conflict(
        "Game.NotJoinable",
        "The game is not in lobby state and cannot be joined.");

    public static readonly Error NicknameTaken = Error.Conflict(
        "Game.NicknameTaken",
        "The nickname is already taken for this game session.");

    public static readonly Error InvalidSessionToken = Error.Unauthorized(
        "Game.InvalidSessionToken",
        "The provided player session token is invalid, revoked, or expired.");

    public static readonly Error RateLimited = Error.RateLimited(
        "Request.RateLimited",
        "Join burst rate limit exceeded.");

    public static readonly Error ValidationFailed = Error.Validation(
        "Validation.Failed",
        "The command parameters do not match the previously recorded command execution.");
}
