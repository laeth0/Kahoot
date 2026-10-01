namespace Kahoot.Api.UnitTests.Features.Auth;

using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.Controllers;
using Kahoot.Api.Options;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.ChangePassword;
using Kahoot.Application.Features.Auth.Login;
using Kahoot.Application.Features.Auth.LogoutAll;
using Kahoot.Application.Features.Auth.Refresh;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Xunit;

public sealed class AuthControllerCookieTests
{
    private readonly RecordingSender _sender = new();
    private readonly IOptions<CorsOptions> _corsOptions = Options.Create(new CorsOptions
    {
        AllowedOrigins = new[] { "https://quiz.example.test" }
    });

    private AuthController CreateController(HttpContext httpContext, int lifetimeDays = 7)
    {
        IOptions<RefreshTokenOptions> refreshOptions = Options.Create(new RefreshTokenOptions
        {
            LifetimeDays = lifetimeDays
        });

        return new AuthController(_sender, refreshOptions, _corsOptions)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    public async Task Login_Success_SetsHardenedRefreshAndCsrfCookiesWithConfiguredLifetime(int lifetimeDays)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/login");
        AuthController controller = CreateController(context, lifetimeDays);

        const string rawRefreshToken = "synthetic-raw-refresh-token-value";
        LoginResponse publicResponse = new(Guid.NewGuid(), "user", "Host", "access.jwt.token", 900);
        LoginResult loginResult = new(publicResponse, rawRefreshToken);
        _sender.RespondWith(Result<LoginResult>.Success(loginResult));

        IActionResult result = await controller.Login(new LoginRequest("user", "pass"), CancellationToken.None);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(publicResponse, okResult.Value);
        Assert.IsNotType<LoginResult>(okResult.Value);

        IList<SetCookieHeaderValue> cookies = SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie);
        Assert.Equal(2, cookies.Count);

        SetCookieHeaderValue refreshCookie = Assert.Single(cookies, c => c.Name == "kahoot_refresh_token");
        Assert.Equal(rawRefreshToken, refreshCookie.Value.ToString());
        Assert.True(refreshCookie.HttpOnly);
        Assert.True(refreshCookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, refreshCookie.SameSite);
        Assert.Equal("/api/auth", refreshCookie.Path.ToString());
        Assert.Equal(TimeSpan.FromDays(lifetimeDays), refreshCookie.MaxAge);

        SetCookieHeaderValue csrfCookie = Assert.Single(cookies, c => c.Name == "kahoot_csrf_token");
        byte[] decodedCsrf = Base64Url.DecodeFromChars(csrfCookie.Value.ToString());
        Assert.Equal(32, decodedCsrf.Length);
        Assert.False(csrfCookie.HttpOnly);
        Assert.True(csrfCookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, csrfCookie.SameSite);
        Assert.Equal("/", csrfCookie.Path.ToString());
        Assert.Equal(TimeSpan.FromDays(lifetimeDays), csrfCookie.MaxAge);
    }

    [Fact]
    public async Task Refresh_Success_RotatesBothCookiesAndReturnsOnlyPublicResponse()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = "https://quiz.example.test";
        context.Request.Headers["X-CSRF-Token"] = "valid-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=old-refresh-token; kahoot_csrf_token=valid-csrf-token";

        AuthController controller = CreateController(context, lifetimeDays: 7);

        const string rotatedRefreshToken = "rotated-raw-refresh-token-value";
        RefreshResponse publicResponse = new(Guid.NewGuid(), "user", "Host", "new.access.jwt.token", 900);
        RefreshResult refreshResult = new(publicResponse, rotatedRefreshToken);
        _sender.RespondWith(Result<RefreshResult>.Success(refreshResult));

        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(publicResponse, okResult.Value);
        Assert.IsNotType<RefreshResult>(okResult.Value);

        IList<SetCookieHeaderValue> cookies = SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie);
        Assert.Equal(2, cookies.Count);

        SetCookieHeaderValue refreshCookie = Assert.Single(cookies, c => c.Name == "kahoot_refresh_token");
        Assert.Equal(rotatedRefreshToken, refreshCookie.Value.ToString());
        Assert.True(refreshCookie.HttpOnly);
        Assert.True(refreshCookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, refreshCookie.SameSite);
        Assert.Equal("/api/auth", refreshCookie.Path.ToString());
        Assert.Equal(TimeSpan.FromDays(7), refreshCookie.MaxAge);

        SetCookieHeaderValue csrfCookie = Assert.Single(cookies, c => c.Name == "kahoot_csrf_token");
        byte[] decodedCsrf = Base64Url.DecodeFromChars(csrfCookie.Value.ToString());
        Assert.Equal(32, decodedCsrf.Length);
        Assert.False(csrfCookie.HttpOnly);
        Assert.True(csrfCookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, csrfCookie.SameSite);
        Assert.Equal("/", csrfCookie.Path.ToString());
        Assert.Equal(TimeSpan.FromDays(7), csrfCookie.MaxAge);
    }

    [Theory]
    [InlineData("logout-all")]
    [InlineData("change-password")]
    public async Task SuccessActions_ClearCookiesWithExpiredDeletionMarkers(string action)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/auth/{action}");
        AuthController controller = CreateController(context);
        _sender.RespondWith(Result.Success());

        IActionResult result = action switch
        {
            "logout-all" => await controller.LogoutAll(CancellationToken.None),
            "change-password" => await controller.ChangePassword(new ChangePasswordRequest("Old123!", "New456!"), CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

        Assert.IsType<NoContentResult>(result);

        IList<SetCookieHeaderValue> cookies = SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie);
        Assert.Equal(2, cookies.Count);

        SetCookieHeaderValue refreshCookie = Assert.Single(cookies, c => c.Name == "kahoot_refresh_token");
        Assert.True(string.IsNullOrEmpty(refreshCookie.Value.ToString()));
        Assert.True(refreshCookie.HttpOnly);
        Assert.True(refreshCookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, refreshCookie.SameSite);
        Assert.Equal("/api/auth", refreshCookie.Path.ToString());
        Assert.True(refreshCookie.Expires < DateTimeOffset.UtcNow);

        SetCookieHeaderValue csrfCookie = Assert.Single(cookies, c => c.Name == "kahoot_csrf_token");
        Assert.True(string.IsNullOrEmpty(csrfCookie.Value.ToString()));
        Assert.False(csrfCookie.HttpOnly);
        Assert.True(csrfCookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, csrfCookie.SameSite);
        Assert.Equal("/", csrfCookie.Path.ToString());
        Assert.True(csrfCookie.Expires < DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task FailureActions_DoNotIssueOrClearCookies()
    {
        DefaultHttpContext context1 = HttpContextFactory.Create(path: "/api/auth/login");
        AuthController controller1 = CreateController(context1);
        _sender.RespondWith(Result.Failure<LoginResult>(AuthErrors.InvalidCredentials));
        await controller1.Login(new LoginRequest("user", "badpass"), CancellationToken.None);
        Assert.Equal(0, context1.Response.Headers.SetCookie.Count);

        DefaultHttpContext context2 = HttpContextFactory.Create(path: "/api/auth/refresh");
        context2.Request.Headers.Origin = "https://quiz.example.test";
        context2.Request.Headers["X-CSRF-Token"] = "token";
        context2.Request.Headers.Cookie = "kahoot_refresh_token=tok; kahoot_csrf_token=token";
        AuthController controller2 = CreateController(context2);
        _sender.RespondWith(Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken));
        await controller2.Refresh(null, CancellationToken.None);
        Assert.Equal(0, context2.Response.Headers.SetCookie.Count);

        DefaultHttpContext context3 = HttpContextFactory.Create(path: "/api/auth/logout-all");
        AuthController controller3 = CreateController(context3);
        _sender.RespondWith(Result.Failure(AuthErrors.Forbidden));
        await controller3.LogoutAll(CancellationToken.None);
        Assert.Equal(0, context3.Response.Headers.SetCookie.Count);

        DefaultHttpContext context4 = HttpContextFactory.Create(path: "/api/auth/change-password");
        AuthController controller4 = CreateController(context4);
        _sender.RespondWith(Result.Failure(AuthErrors.InvalidCredentials));
        await controller4.ChangePassword(new ChangePasswordRequest("Wrong!", "NewPass123!"), CancellationToken.None);
        Assert.Equal(0, context4.Response.Headers.SetCookie.Count);
    }
}
