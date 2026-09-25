using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Auth;

public static class AuthErrors
{
    public static readonly Error UsernameUnavailable = Error.Conflict(
        "Auth.UsernameUnavailable",
        "The specified username is already in use.");

    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.InvalidCredentials",
        "The provided username or password was incorrect.");

    public static readonly Error RateLimited = Error.RateLimited(
        "Request.RateLimited",
        "Too many requests. Please try again later.");

    public static readonly Error InvalidRefreshToken = Error.Unauthorized(
        "Auth.InvalidRefreshToken",
        "Invalid, expired, or revoked refresh token.");

    public static readonly Error RefreshTokenReuse = Error.Unauthorized(
        "Auth.RefreshTokenReuse",
        "Refresh token has already been consumed. Session revoked.");

    public static readonly Error RefreshRace = Error.Conflict(
        "Auth.RefreshRace",
        "Concurrent refresh in progress. Please retry or wait for completion.");

    public static readonly Error Forbidden = Error.Forbidden(
        "Auth.Forbidden",
        "Access denied due to invalid CSRF token or disallowed origin.");

    public static readonly Error Unauthorized = Error.Unauthorized(
        "Auth.Unauthorized",
        "Authentication is required to access this resource, or token is invalid.");
}
