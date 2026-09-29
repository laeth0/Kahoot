using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Auth;

public static class AuthErrors
{
    // Global Identity Conflict - Enforces unique username constraint across host and administrator accounts
    public static readonly Error UsernameUnavailable = Error.Conflict(
        "Auth.UsernameUnavailable",
        "The specified username is already in use.");

    // Account Enumeration Defense - Treats non-existent users and wrong passwords identically to prevent username harvesting
    public static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.InvalidCredentials",
        "The provided username or password was incorrect.");

    // Resource Exhaustion & Brute-Force Throttling - Returned when IP, username, or hashing concurrency limit is exceeded
    public static readonly Error RateLimited = Error.RateLimited(
        "Request.RateLimited",
        "Too many requests. Please try again later.");

    // Session Invalidation - Rejects expired, tampered, or revoked tokens (AUTH-ERR-003)
    public static readonly Error InvalidRefreshToken = Error.Unauthorized(
        "Auth.InvalidRefreshToken",
        "Invalid, expired, or revoked refresh token.");

    // Malicious Token Reuse Detection - Replaying a previously rotated refresh token after grace period triggers session revocation
    public static readonly Error RefreshTokenReuse = Error.Unauthorized(
        "Auth.RefreshTokenReuse",
        "Refresh token has already been consumed. Session revoked.");

    // Multi-Tab Concurrency Race (409 Conflict) - Signals parallel refresh requests within grace window without destroying session
    public static readonly Error RefreshRace = Error.Conflict(
        "Auth.RefreshRace",
        "Concurrent refresh in progress. Please retry or wait for completion.");

    // CSRF & Origin Protection - Rejects requests when double-submit CSRF cookie token or Origin/Referer check fails
    public static readonly Error Forbidden = Error.Forbidden(
        "Auth.Forbidden",
        "Access denied due to invalid CSRF token or disallowed origin.");

    // Access Guard - Returned when authentication header is missing or access token is malformed/invalid
    public static readonly Error Unauthorized = Error.Unauthorized(
        "Auth.Unauthorized",
        "Authentication is required to access this resource, or token is invalid.");
}
