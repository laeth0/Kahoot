using Kahoot.Api.Common;
using Kahoot.Api.Contracts;
using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Authentication.Login;
using Kahoot.Application.Authentication.Logout;
using Kahoot.Application.Authentication.Refresh;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kahoot.Api.Controllers;

[AllowAnonymous]
[Route("api/auth")]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public sealed class AuthController(
    ISender sender,
    IWebHostEnvironment environment,
    IConfiguration configuration) : ApiControllerBase
{
    private const string RefreshCookieName = "kahoot_refresh_token";

    [HttpPost("login")]
    [ProducesResponseType<HostAuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var command = new LoginCommand(request.Username, request.Password);
        var result = await sender.Send(command, cancellationToken);

        return ToActionResult(result, tokens =>
        {
            AppendRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
            return Ok(new HostAuthResponse(tokens.HostId, tokens.Username, tokens.AccessToken, tokens.AccessTokenExpiresAt));
        });
    }

    [HttpPost("refresh")]
    [ProducesResponseType<HostAuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Refresh(RefreshRequest? request, CancellationToken cancellationToken)
    {
        string? refreshToken = Request.Cookies[RefreshCookieName] ?? request?.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return ToActionResult(Kahoot.Domain.Common.Result.Failure<AuthenticationResponse>(AuthenticationErrors.InvalidRefreshToken));
        }

        if (Request.Cookies.ContainsKey(RefreshCookieName) && !ValidateCsrfAndOrigin())
        {
            return Forbid();
        }

        var command = new RefreshTokenCommand(refreshToken);
        var result = await sender.Send(command, cancellationToken);

        return ToActionResult(result, tokens =>
        {
            AppendRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
            return Ok(new HostAuthResponse(tokens.HostId, tokens.Username, tokens.AccessToken, tokens.AccessTokenExpiresAt));
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Logout(LogoutRequest? request, CancellationToken cancellationToken)
    {
        string? refreshToken = Request.Cookies[RefreshCookieName] ?? request?.RefreshToken;

        if (Request.Cookies.ContainsKey(RefreshCookieName) && !ValidateCsrfAndOrigin())
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var command = new LogoutCommand(refreshToken);
            await sender.Send(command, cancellationToken);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    private void AppendRefreshCookie(string token, DateTimeOffset expiresAt)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = expiresAt
        });
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth"
        });
    }

    private bool ValidateCsrfAndOrigin()
    {
        bool hasCsrfHeader = Request.Headers.TryGetValue("X-Requested-With", out var requestedWith) && requestedWith == "XMLHttpRequest"
            || Request.Headers.TryGetValue("X-CSRF-Token", out var csrfToken) && csrfToken == "1";

        if (!hasCsrfHeader)
        {
            return false;
        }

        if (Request.Headers.TryGetValue("Origin", out var originHeader) && !string.IsNullOrWhiteSpace(originHeader))
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? configuration["Cors:AllowedOrigins"]?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];

            if (allowedOrigins.Length > 0 && !allowedOrigins.Contains(originHeader.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
