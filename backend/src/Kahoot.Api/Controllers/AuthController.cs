using Kahoot.Api.Options;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.ChangePassword;
using Kahoot.Application.Features.Auth.Login;
using Kahoot.Application.Features.Auth.Logout;
using Kahoot.Application.Features.Auth.LogoutAll;
using Kahoot.Application.Features.Auth.Refresh;
using Kahoot.Application.Features.Auth.Register;
using Kahoot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Kahoot.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ApiController
{
    private const string RefreshTokenCookieName = "kahoot_refresh_token";
    private const string RefreshTokenCookiePath = "/api/auth";

    private readonly ISender _sender;
    private readonly IOptions<RefreshTokenOptions> _refreshTokenOptions;
    private readonly IOptions<CorsOptions> _corsOptions;

    public AuthController(
        ISender sender,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        IOptions<CorsOptions> corsOptions)
    {
        _sender = sender;
        _refreshTokenOptions = refreshTokenOptions;
        _corsOptions = corsOptions;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(request.Username, request.Password);
        var result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Created(string.Empty, result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var command = new LoginCommand(request.Username, request.Password, ipAddress);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        SetRefreshTokenCookie(result.Value.RawRefreshToken);
        return Ok(result.Value.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshRequest? request,
        CancellationToken cancellationToken)
    {
        bool hasCookie = Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken) &&
                         !string.IsNullOrWhiteSpace(cookieToken);

        if (!ValidateCsrfAndOrigin(hasCookie))
        {
            return Problem(AuthErrors.Forbidden);
        }

        var rawRefreshToken = hasCookie ? cookieToken : request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Problem(AuthErrors.InvalidRefreshToken);
        }

        var command = new RefreshCommand(rawRefreshToken);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        SetRefreshTokenCookie(result.Value.RawRefreshToken);
        return Ok(result.Value.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        bool hasCookie = Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken) &&
                         !string.IsNullOrWhiteSpace(cookieToken);

        if (!ValidateCsrfAndOrigin(hasCookie))
        {
            return Problem(AuthErrors.Forbidden);
        }

        var command = new LogoutCommand(hasCookie ? cookieToken : null);
        var result = await _sender.Send(command, cancellationToken);

        ClearRefreshTokenCookie();

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize(Roles = $"{nameof(UserRole.Host)},{nameof(UserRole.SystemAdmin)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var command = new LogoutAllCommand();
        var result = await _sender.Send(command, cancellationToken);

        ClearRefreshTokenCookie();

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize(Roles = $"{nameof(UserRole.Host)},{nameof(UserRole.SystemAdmin)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ChangePasswordCommand(request.CurrentPassword, request.NewPassword);
        var result = await _sender.Send(command, cancellationToken);

        ClearRefreshTokenCookie();

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        return NoContent();
    }

    private bool ValidateCsrfAndOrigin(bool isCookieAuth)
    {
        // 1. Origin / Referer validation if present
        string? origin = Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin) && Request.Headers.TryGetValue("Referer", out var refererValues))
        {
            var referer = refererValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
            {
                origin = refererUri.GetLeftPart(UriPartial.Authority);
            }
        }

        if (!string.IsNullOrWhiteSpace(origin))
        {
            if (!IsOriginAllowed(origin, _corsOptions.Value.AllowedOrigins))
            {
                return false;
            }
        }

        // 2. Custom header CSRF mitigation for browser cookie-authenticated requests
        if (isCookieAuth)
        {
            if (!Request.Headers.TryGetValue("X-CSRF-Token", out var csrfToken) ||
                string.IsNullOrWhiteSpace(csrfToken))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsOriginAllowed(string origin, string[] allowedOrigins)
    {
        if (allowedOrigins.Any(allowed => string.Equals(allowed.TrimEnd('/'), origin.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            if (string.Equals(originUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void SetRefreshTokenCookie(string rawRefreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = RefreshTokenCookiePath,
            MaxAge = TimeSpan.FromDays(_refreshTokenOptions.Value.LifetimeDays)
        };

        Response.Cookies.Append(RefreshTokenCookieName, rawRefreshToken, cookieOptions);
    }

    private void ClearRefreshTokenCookie()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = RefreshTokenCookiePath
        };

        Response.Cookies.Delete(RefreshTokenCookieName, cookieOptions);
    }
}
