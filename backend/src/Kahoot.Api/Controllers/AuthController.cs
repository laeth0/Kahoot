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
using Microsoft.Extensions.Primitives;

namespace Kahoot.Api.Controllers;

// Authentication and Session Controller - Manages user registration, credential login, refresh token rotation, and multi-tenant session invalidation.
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

    // Host Registration Endpoint - Creates new Host tenant account with Argon2id password hashing and unique username validation.
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
        RegisterCommand command = new RegisterCommand(request.Username, request.Password);
        Result<RegisterResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Created(string.Empty, result.Value);
        }

        return Problem(result.Error);
    }

    // Host Credential Login Endpoint - Verifies credentials, issues short-lived JWT, sets hardened HttpOnly refresh cookie, and issues CSRF token.
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
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        LoginCommand command = new LoginCommand(request.Username, request.Password, ipAddress);
        Result<LoginResult> result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        // HttpOnly Cookie Transport - Transmits refresh token exclusively via hardened cookie to eliminate XSS theft.
        SetRefreshTokenCookie(result.Value.RawRefreshToken);
        SetCsrfCookie();
        return Ok(result.Value.Response);
    }

    // Refresh Token Rotation Endpoint - Exchanges current refresh token for a newly rotated refresh token and fresh JWT access token.
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
        bool hasCookie = Request.Cookies.TryGetValue(RefreshTokenCookieName, out string? cookieToken);

        if (!ValidateCsrfAndOrigin(hasCookie))
        {
            return Problem(AuthErrors.Forbidden);
        }

        string? rawRefreshToken = hasCookie ? cookieToken : request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Problem(AuthErrors.InvalidRefreshToken);
        }

        RefreshCommand command = new RefreshCommand(rawRefreshToken);
        Result<RefreshResult> result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        SetRefreshTokenCookie(result.Value.RawRefreshToken);
        SetCsrfCookie();
        return Ok(result.Value.Response);
    }

    // Single Device Logout Endpoint - Revokes current refresh token family and clears local authentication cookies.
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        bool hasCookie = Request.Cookies.TryGetValue(RefreshTokenCookieName, out string? cookieToken);

        if (!ValidateCsrfAndOrigin(hasCookie))
        {
            return Problem(AuthErrors.Forbidden);
        }

        LogoutCommand command = new LogoutCommand(hasCookie ? cookieToken : null);
        Result result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        ClearAuthCookies();
        return NoContent();
    }

    // All-Device Logout Endpoint - Increments TokenSecurityVersion to invalidate all extant bearer tokens and revokes all refresh tokens.
    [HttpPost("logout-all")]
    [Authorize(Roles = $"{nameof(UserRole.Host)},{nameof(UserRole.SystemAdmin)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        LogoutAllCommand command = new LogoutAllCommand();
        Result result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        ClearAuthCookies();
        return NoContent();
    }

    // Change Password Endpoint - Verifies current password, re-hashes new password with Argon2id, and revokes all other sessions.
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
        ChangePasswordCommand command = new ChangePasswordCommand(request.CurrentPassword, request.NewPassword);
        Result result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Problem(result.Error);
        }

        ClearAuthCookies();
        return NoContent();
    }

    // CSRF & Origin Validation - Enforces double-submit cookie verification (FixedTimeEquals) and Origin header whitelist.
    private bool ValidateCsrfAndOrigin(bool isCookieAuth)
    {
        if (!Request.Headers.TryGetValue(CsrfHeaderName, out StringValues csrfHeader) ||
            csrfHeader.Count != 1 || string.IsNullOrWhiteSpace(csrfHeader[0]))
        {
            return false;
        }

        if (isCookieAuth)
        {
            if (!Request.Cookies.TryGetValue(CsrfCookieName, out string? csrfCookie) ||
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

        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? originUri) ||
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
                Uri.TryCreate(allowed, UriKind.Absolute, out Uri? allowedUri) && SameOrigin(origin, allowedUri)))
        {
            return true;
        }

        return Uri.TryCreate($"{Request.Scheme}://{Request.Host}", UriKind.Absolute, out Uri? requestOrigin) &&
               SameOrigin(origin, requestOrigin);
    }

    private static bool SameOrigin(Uri first, Uri second)
    {
        return string.Equals(first.Scheme, second.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(first.IdnHost, second.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               first.Port == second.Port;
    }

    // Cookie Hardening - Scopes refresh token to HttpOnly, Secure, SameSite=Lax, and /api/auth path
    private void SetRefreshTokenCookie(string rawRefreshToken)
    {
        CookieOptions cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = RefreshTokenCookiePath,
            MaxAge = TimeSpan.FromDays(_refreshTokenOptions.Value.LifetimeDays)
        };

        Response.Cookies.Append(RefreshTokenCookieName, rawRefreshToken, cookieOptions);
    }

    // Double-Submit CSRF Cookie - Sets readable cookie token for client script to echo back in X-CSRF-Token header
    private void SetCsrfCookie()
    {
        CookieOptions cookieOptions = new CookieOptions
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
