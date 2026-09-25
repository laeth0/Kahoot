using System.Buffers.Text;
using System.Security.Cryptography;
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
    private const string CsrfCookieName = "kahoot_csrf_token";
    private const string CsrfHeaderName = "X-CSRF-Token";

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
        SetCsrfCookie();
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
        bool hasCookie = Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken);

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
        SetCsrfCookie();
        return Ok(result.Value.Response);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        bool hasCookie = Request.Cookies.TryGetValue(RefreshTokenCookieName, out var cookieToken);

        if (!ValidateCsrfAndOrigin(hasCookie))
        {
            return Problem(AuthErrors.Forbidden);
        }

        var command = new LogoutCommand(hasCookie ? cookieToken : null);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        ClearAuthCookies();
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

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        ClearAuthCookies();
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

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        ClearAuthCookies();
        return NoContent();
    }

    private bool ValidateCsrfAndOrigin(bool isCookieAuth)
    {
        if (!Request.Headers.TryGetValue(CsrfHeaderName, out var csrfHeader) ||
            csrfHeader.Count != 1 || string.IsNullOrWhiteSpace(csrfHeader[0]))
        {
            return false;
        }

        if (isCookieAuth)
        {
            if (!Request.Cookies.TryGetValue(CsrfCookieName, out var csrfCookie) ||
                string.IsNullOrEmpty(csrfCookie) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(csrfHeader[0]!),
                    System.Text.Encoding.UTF8.GetBytes(csrfCookie)))
            {
                return false;
            }
        }

        string? origin = Request.Headers.Origin.ToString();
        bool fromOriginHeader = !string.IsNullOrEmpty(origin);
        if (!fromOriginHeader)
        {
            origin = Request.Headers.Referer.ToString();
        }

        if (string.IsNullOrEmpty(origin))
        {
            return !isCookieAuth;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) ||
            originUri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(originUri.UserInfo) ||
            (fromOriginHeader && origin != originUri.GetLeftPart(UriPartial.Authority)))
        {
            return false;
        }

        return IsOriginAllowed(originUri);
    }

    private bool IsOriginAllowed(Uri origin)
    {
        if (_corsOptions.Value.AllowedOrigins.Any(allowed =>
                Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri) && SameOrigin(origin, allowedUri)))
        {
            return true;
        }

        return Uri.TryCreate($"{Request.Scheme}://{Request.Host}", UriKind.Absolute, out var requestOrigin) &&
               SameOrigin(origin, requestOrigin);
    }

    private static bool SameOrigin(Uri first, Uri second)
    {
        return string.Equals(first.Scheme, second.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.IdnHost, second.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               first.Port == second.Port;
    }

    private void SetRefreshTokenCookie(string rawRefreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = RefreshTokenCookiePath,
            MaxAge = TimeSpan.FromDays(_refreshTokenOptions.Value.LifetimeDays)
        };

        Response.Cookies.Append(RefreshTokenCookieName, rawRefreshToken, cookieOptions);
    }

    private void SetCsrfCookie()
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = TimeSpan.FromDays(_refreshTokenOptions.Value.LifetimeDays)
        };

        Response.Cookies.Append(CsrfCookieName, Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)), cookieOptions);
    }

    private void ClearAuthCookies()
    {
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = RefreshTokenCookiePath
        });

        Response.Cookies.Delete(CsrfCookieName, new CookieOptions
        {
            HttpOnly = false,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }
}
