using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Auth;

public static class AuthErrors
{
    public static readonly Error UsernameUnavailable = Error.Conflict(
        "Auth.UsernameUnavailable",
        "The specified username is already in use.");
}
