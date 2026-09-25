using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth.Login;
using Kahoot.Application.Features.Auth.Register;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public AuthController(ISender sender, IOptions<RefreshTokenOptions> refreshTokenOptions)
    {
        _sender = sender;
        _refreshTokenOptions = refreshTokenOptions;
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
}
