using Kahoot.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Controllers;

// Base API Controller - Standardizes error handling and RFC 7807 ProblemDetails mapping across all REST endpoints.
[ApiController]
public abstract class ApiController : ControllerBase
{
    // RFC 7807 ProblemDetails Mapping - Converts domain Result error types and codes into standardized HTTP status codes and payloads.
    protected IActionResult Problem(Error error)
    {
        // HTTP Status Code Projection - Maps domain ErrorType enum to canonical HTTP response status code.
        int statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.RateLimited => StatusCodes.Status429TooManyRequests,
            ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.TooLarge => StatusCodes.Status413PayloadTooLarge,
            ErrorType.UnsupportedType => StatusCodes.Status415UnsupportedMediaType,
            _ => StatusCodes.Status500InternalServerError
        };

        string title = error.Code switch
        {
            "Auth.InvalidCredentials" => "Invalid credentials",
            "Auth.InvalidRefreshToken" => "Invalid refresh token",
            "Auth.RefreshTokenReuse" => "Refresh token reuse detected",
            "Auth.RefreshRace" => "Concurrent refresh",
            "Auth.UsernameUnavailable" => "Username unavailable",
            "Auth.Unauthorized" => "Unauthorized",
            "Auth.Forbidden" => "Forbidden",
            "Request.RateLimited" => "Rate limited",
            "Service.Unavailable" => "Service unavailable",
            "Validation.Failed" => "Validation failed",
            "Image.InvalidImage" => "Invalid image",
            "Image.TooLarge" => "Payload too large",
            "Image.UnsupportedType" => "Unsupported media type",
            "Image.StorageUnavailable" => "Storage unavailable",
            "Image.NotFound" => "Image not found",
            "Quiz.NotFound" => "Quiz not found",
            "Quiz.QuestionNotFound" => "Question not found",
            "Quiz.InUse" => "Quiz in use",
            "Quiz.HasSessions" => "Quiz has sessions",
            "Quiz.ConcurrentModification" => "Concurrent modification",
            "Quiz.QuestionSetMismatch" => "Question set mismatch",
            "Quiz.InvalidImageReference" => "Invalid image reference",
            "Game.NotFound" => "Game not found",
            "Game.InvalidStateTransition" => "Invalid state transition",
            "Game.NoMoreQuestions" => "No more questions",
            "Game.ConcurrentModification" => "Concurrent modification",
            "Game.PinUnavailable" => "PIN unavailable",
            "Game.AnswerTooLate" => "Answer too late",
            "Game.ArchiveImmutable" => "Archive immutable",
            "Game.Full" => "Game full",
            "Game.InvalidPin" => "Invalid PIN",
            "Game.ParticipantNotFound" => "Participant not found",
            "Game.NotJoinable" => "Game not joinable",
            "Game.NicknameTaken" => "Nickname taken",
            "Game.InvalidSessionToken" => "Invalid session token",
            "Game.NotCurrentQuestion" => "Question not current",
            "Game.InvalidChoices" => "Invalid choices",
            "Game.ParticipantRemoved" => "Participant removed",
            "Game.Unavailable" => "Game unavailable",
            "Game.TooManyAnswerAttempts" => "Too many answer attempts",
            _ => error.Code
        };

        Dictionary<string, object?> extensions = new()
        {
            ["code"] = error.Code,
            ["requestId"] = HttpContext.TraceIdentifier
        };

        // Distributed Trace Correlation (OPS-OBS-004) - Includes 32-character hexadecimal traceId in ProblemDetails if ambient Activity exists
        if (System.Diagnostics.Activity.Current is not null)
        {
            extensions["traceId"] = System.Diagnostics.Activity.Current.TraceId.ToString();
        }

        return Problem(
            detail: error.Description,
            instance: HttpContext.Request.Path,
            statusCode: statusCode,
            title: title,
            type: $"https://api.kahoot-saas.local/errors/{error.Code}",
            extensions: extensions);
    }
}
