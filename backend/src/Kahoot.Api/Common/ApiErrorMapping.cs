using System.Collections.Frozen;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;

namespace Kahoot.Api.Common;

internal static class ApiErrorMapping
{
    private const string AuthCodePrefix = "Auth.";
    private const string NotFoundCodeSuffix = ".NotFound";

    private static readonly FrozenSet<string> NotFoundCodes = new[]
    {
        GameErrors.InvalidPin.Code
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ConflictCodes = new[]
    {
        GameErrors.InvalidStateTransition.Code,
        GameErrors.ConcurrentModification.Code,
        GameErrors.NicknameTaken.Code,
        GameErrors.PinUnavailable.Code,
        GameErrors.NoMoreQuestions.Code,
        GameErrors.NotJoinable.Code,
        GameErrors.QuizNotPublished.Code,
        QuizErrors.InUse.Code,
        QuizErrors.HasSessions.Code,
        QuizErrors.ConcurrentModification.Code
    }.ToFrozenSet(StringComparer.Ordinal);

    public static int ToStatusCode(Error error)
    {
        if (error is ValidationError)
        {
            return StatusCodes.Status400BadRequest;
        }

        if (error.Code == SharedErrors.Forbidden.Code)
        {
            return StatusCodes.Status403Forbidden;
        }

        if (error.Code.StartsWith(AuthCodePrefix, StringComparison.Ordinal))
        {
            return StatusCodes.Status401Unauthorized;
        }

        if (error.Code.EndsWith(NotFoundCodeSuffix, StringComparison.Ordinal) || NotFoundCodes.Contains(error.Code))
        {
            return StatusCodes.Status404NotFound;
        }

        if (ConflictCodes.Contains(error.Code))
        {
            return StatusCodes.Status409Conflict;
        }

        return StatusCodes.Status400BadRequest;
    }
}
