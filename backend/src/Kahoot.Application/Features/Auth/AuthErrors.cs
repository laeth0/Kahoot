using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Auth;

public static class AuthErrors
{
    public static readonly Error UsernameUnavailable = Error.Conflict(
        "Auth.UsernameUnavailable",
        "The specified username is already in use.");

    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.InvalidCredentials",
        "Invalid username or password.");

    public static readonly Error RateLimited = Error.RateLimited(
        "Request.RateLimited",
        "Too many requests. Please try again later.");
}
