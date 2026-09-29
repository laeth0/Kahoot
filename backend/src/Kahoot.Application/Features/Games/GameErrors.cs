using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Games;

public static class GameErrors
{
    // Session Lookup - Returned when game session does not exist or caller lacks tenant access
    public static readonly Error NotFound = Error.NotFound(
        "Game.NotFound",
        "The requested game session was not found.");

    // Finite State Machine Invariant - Rejects illegal transitions per the authoritative 6-state game lifecycle
    public static readonly Error InvalidStateTransition = Error.Conflict(
        "Game.InvalidStateTransition",
        "The requested state transition is not permitted from the current game state.");

    // Question Sequence Bounds - Prevents advancing past final question index in snapshot
    public static readonly Error NoMoreQuestions = Error.Conflict(
        "Game.NoMoreQuestions",
        "There are no more questions remaining in the quiz.");

    // Optimistic Concurrency Control (OCC) - Detects concurrent game state mutations via row versioning
    public static readonly Error ConcurrentModification = Error.Conflict(
        "Game.ConcurrentModification",
        "The game session was modified by another operation. Please refresh and try again.");

    // Collision Resistance - Exhausted PIN generation retry attempts across active games namespace
    public static readonly Error PinUnavailable = Error.Conflict(
        "Game.PinUnavailable",
        "Failed to generate a unique PIN for the game session after maximum retry attempts.");

    // Authoritative Server Timing - Rejects submissions arriving after question deadline cutoff
    public static readonly Error AnswerTooLate = Error.Conflict(
        "Game.AnswerTooLate",
        "The answer was submitted after the question deadline expired.");

    // Append-Only Audit Integrity - Finished games are immutable historical archives
    public static readonly Error ArchiveImmutable = Error.Conflict(
        "Game.ArchiveImmutable",
        "The game session is finished and its historical archive cannot be modified.");

    // Capacity Guard (LOBBY-CAP-001) - Enforces hard limit of 500 participants per game session
    public static readonly Error Full = Error.Conflict(
        "Game.Full",
        "The game session has reached its maximum participant capacity.");

    // PIN Validation - Rejects invalid, expired, or non-lobby game PIN lookups
    public static readonly Error InvalidPin = Error.NotFound(
        "Game.InvalidPin",
        "The provided PIN does not match any active game session.");

    // Participant Membership - Participant record missing from game session
    public static readonly Error ParticipantNotFound = Error.NotFound(
        "Game.ParticipantNotFound",
        "The participant was not found in this game session.");

    // Lobby Phase Guard - Only games in Lobby state accept player joins
    public static readonly Error NotJoinable = Error.Conflict(
        "Game.NotJoinable",
        "The game is not in lobby state and cannot be joined.");

    // Nickname Uniqueness & Tombstones - Prevents duplicate or previously kicked nicknames in the session
    public static readonly Error NicknameTaken = Error.Conflict(
        "Game.NicknameTaken",
        "The nickname is already taken for this game session.");

    // Player Session Authentication - Cryptographically validates HMAC player session token
    public static readonly Error InvalidSessionToken = Error.Unauthorized(
        "Game.InvalidSessionToken",
        "The provided player session token is invalid, revoked, or expired.");

    // Token Bucket Rate Limiting (LOBBY-RAT-001) - Throttles burst join traffic per game session
    public static readonly Error RateLimited = Error.RateLimited(
        "Request.RateLimited",
        "Join burst rate limit exceeded.");

    // Command Idempotency Verification - Rejects replayed commands whose payload hashes differ from original
    public static readonly Error ValidationFailed = Error.Validation(
        "Validation.Failed",
        "The command parameters do not match the previously recorded command execution.");
}
