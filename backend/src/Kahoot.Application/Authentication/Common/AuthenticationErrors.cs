using Kahoot.Domain.Common;

namespace Kahoot.Application.Authentication.Common;

public static class AuthenticationErrors
{
    public static readonly Error InvalidCredentials =
        new("Auth.InvalidCredentials", "The username or password is incorrect.");

    public static readonly Error UsernameTaken =
        new("Auth.UsernameTaken", "That username is already registered.");

    public static readonly Error InvalidRefreshToken =
        new("Auth.InvalidRefreshToken", "The refresh token is invalid or has expired.");

    public static readonly Error RefreshTokenReuseDetected =
        new("Auth.RefreshTokenReuse", "The refresh token has already been used; the session has been revoked.");
}
