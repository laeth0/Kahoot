namespace Kahoot.Api.UnitTests.Features.Auth;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.Controllers;
using Kahoot.Api.Options;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Auth.Logout;
using Kahoot.Application.Features.Auth.Refresh;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;

public sealed class AuthControllerCsrfTests
{
    private const string AllowedOrigin = "https://quiz.example.test";
    private readonly RecordingSender _sender = new();
    private readonly IOptions<RefreshTokenOptions> _refreshOptions = Options.Create(new RefreshTokenOptions { LifetimeDays = 7 });
    private readonly IOptions<CorsOptions> _corsOptions = Options.Create(new CorsOptions
    {
        AllowedOrigins = new[] { AllowedOrigin }
    });

    private AuthController CreateController(HttpContext httpContext)
    {
        return new AuthController(_sender, _refreshOptions, _corsOptions)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Theory]
    [InlineData("refresh")]
    [InlineData("logout")]
    public async Task Guard_MissingEmptyWhitespaceOrMultipleCsrfHeader_IsRejected(string action)
    {
        // 1. Missing header
        DefaultHttpContext ctxMissing = HttpContextFactory.Create(path: $"/api/auth/{action}");
        ctxMissing.Request.Headers.Origin = AllowedOrigin;
        ctxMissing.Request.Headers.Cookie = "kahoot_refresh_token=valid-tok; kahoot_csrf_token=csrf-tok";
        AuthController ctrlMissing = CreateController(ctxMissing);
        IActionResult resMissing = action == "refresh"
            ? await ctrlMissing.Refresh(null, CancellationToken.None)
            : await ctrlMissing.Logout(CancellationToken.None);
        AssertForbidden(resMissing, ctxMissing);

        // 2. Empty header
        DefaultHttpContext ctxEmpty = HttpContextFactory.Create(path: $"/api/auth/{action}");
        ctxEmpty.Request.Headers.Origin = AllowedOrigin;
        ctxEmpty.Request.Headers["X-CSRF-Token"] = string.Empty;
        ctxEmpty.Request.Headers.Cookie = "kahoot_refresh_token=valid-tok; kahoot_csrf_token=csrf-tok";
        AuthController ctrlEmpty = CreateController(ctxEmpty);
        IActionResult resEmpty = action == "refresh"
            ? await ctrlEmpty.Refresh(null, CancellationToken.None)
            : await ctrlEmpty.Logout(CancellationToken.None);
        AssertForbidden(resEmpty, ctxEmpty);

        // 3. Whitespace header
        DefaultHttpContext ctxWhitespace = HttpContextFactory.Create(path: $"/api/auth/{action}");
        ctxWhitespace.Request.Headers.Origin = AllowedOrigin;
        ctxWhitespace.Request.Headers["X-CSRF-Token"] = "   ";
        ctxWhitespace.Request.Headers.Cookie = "kahoot_refresh_token=valid-tok; kahoot_csrf_token=csrf-tok";
        AuthController ctrlWhitespace = CreateController(ctxWhitespace);
        IActionResult resWhitespace = action == "refresh"
            ? await ctrlWhitespace.Refresh(null, CancellationToken.None)
            : await ctrlWhitespace.Logout(CancellationToken.None);
        AssertForbidden(resWhitespace, ctxWhitespace);

        // 4. Multiple headers
        DefaultHttpContext ctxMultiple = HttpContextFactory.Create(path: $"/api/auth/{action}");
        ctxMultiple.Request.Headers.Origin = AllowedOrigin;
        ctxMultiple.Request.Headers["X-CSRF-Token"] = new StringValues(new[] { "token1", "token2" });
        ctxMultiple.Request.Headers.Cookie = "kahoot_refresh_token=valid-tok; kahoot_csrf_token=token1";
        AuthController ctrlMultiple = CreateController(ctxMultiple);
        IActionResult resMultiple = action == "refresh"
            ? await ctrlMultiple.Refresh(null, CancellationToken.None)
            : await ctrlMultiple.Logout(CancellationToken.None);
        AssertForbidden(resMultiple, ctxMultiple);

        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Guard_CookieAuth_MissingCsrfCookie_IsRejected()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = AllowedOrigin;
        context.Request.Headers["X-CSRF-Token"] = "valid-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=valid-refresh-token";

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        AssertForbidden(result, context);
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData("csrf-token-1", "csrf-token-2")] // same length
    [InlineData("csrf-token-1", "csrf-token-1-longer")] // different length
    public async Task Guard_CookieAuth_MismatchedCsrfCookieAndHeader_IsRejected(string headerToken, string cookieToken)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = AllowedOrigin;
        context.Request.Headers["X-CSRF-Token"] = headerToken;
        context.Request.Headers.Cookie = $"kahoot_refresh_token=valid-refresh-token; kahoot_csrf_token={cookieToken}";

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        AssertForbidden(result, context);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Guard_CookieAuth_MatchingCsrf_WithoutOriginOrReferer_IsRejected()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers["X-CSRF-Token"] = "matching-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=valid-refresh-token; kahoot_csrf_token=matching-csrf-token";

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        AssertForbidden(result, context);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Guard_CookieAuth_MatchingCsrf_AndAllowedOrigin_IsAdmitted()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = AllowedOrigin;
        context.Request.Headers["X-CSRF-Token"] = "matching-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=valid-refresh-token; kahoot_csrf_token=matching-csrf-token";

        RefreshResponse expectedResponse = new(Guid.NewGuid(), "user", "Host", "new.access.token", 900);
        _sender.RespondWith(Result<RefreshResult>.Success(new RefreshResult(expectedResponse, "new-refresh-token")));

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expectedResponse, okResult.Value);
        Assert.Single(_sender.Requests);
    }

    [Fact]
    public async Task Guard_CookieAuth_MatchingCsrf_AndRequestHostOrigin_IsAdmitted()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.test");
        context.Request.Headers.Origin = "https://api.example.test";
        context.Request.Headers["X-CSRF-Token"] = "matching-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=valid-refresh-token; kahoot_csrf_token=matching-csrf-token";

        RefreshResponse expectedResponse = new(Guid.NewGuid(), "user", "Host", "new.access.token", 900);
        _sender.RespondWith(Result<RefreshResult>.Success(new RefreshResult(expectedResponse, "new-refresh-token")));

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(_sender.Requests);
    }

    [Fact]
    public async Task Guard_NonCookieAuth_SingleHeader_WithoutOriginOrReferer_IsAdmitted()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers["X-CSRF-Token"] = "standalone-csrf-token";

        RefreshResponse expectedResponse = new(Guid.NewGuid(), "user", "Host", "new.access.token", 900);
        _sender.RespondWith(Result<RefreshResult>.Success(new RefreshResult(expectedResponse, "new-refresh-token")));

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(new RefreshRequest("body-refresh-token"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(_sender.Requests);
        RefreshCommand cmd = Assert.IsType<RefreshCommand>(_sender.Requests[0]);
        Assert.Equal("body-refresh-token", cmd.RawRefreshToken);
    }

    [Fact]
    public async Task Guard_AllowedReferer_WhenOriginAbsent_IsAdmitted()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Referer = "https://quiz.example.test/auth/login?redirect=%2Fdashboard";
        context.Request.Headers["X-CSRF-Token"] = "matching-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=valid-refresh-token; kahoot_csrf_token=matching-csrf-token";

        RefreshResponse expectedResponse = new(Guid.NewGuid(), "user", "Host", "new.access.token", 900);
        _sender.RespondWith(Result<RefreshResult>.Success(new RefreshResult(expectedResponse, "new-refresh-token")));

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(_sender.Requests);
    }

    [Fact]
    public async Task Guard_DisallowedOrigin_WithAllowedReferer_IsRejectedWithoutFallback()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = "https://evil.attacker.test";
        context.Request.Headers.Referer = "https://quiz.example.test/auth/login";
        context.Request.Headers["X-CSRF-Token"] = "matching-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=valid-refresh-token; kahoot_csrf_token=matching-csrf-token";

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        AssertForbidden(result, context);
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData("https://quiz.example.test/")]
    [InlineData("https://quiz.example.test/path")]
    [InlineData("https://quiz.example.test?foo=bar")]
    [InlineData("https://quiz.example.test#fragment")]
    [InlineData("https://user:pass@quiz.example.test")]
    [InlineData("quiz.example.test")]
    [InlineData("ftp://quiz.example.test")]
    public async Task Guard_OriginWithTrailingSlashPathQueryOrInvalidFormat_IsRejected(string invalidOrigin)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = invalidOrigin;
        context.Request.Headers["X-CSRF-Token"] = "matching-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=valid-refresh-token; kahoot_csrf_token=matching-csrf-token";

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        AssertForbidden(result, context);
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData("https://quiz.example.test.attacker.test")] // suffix attack
    [InlineData("http://quiz.example.test")] // different scheme
    [InlineData("https://quiz.example.test:8080")] // non-default port
    public async Task Guard_OriginMismatchSchemeHostOrPort_IsRejected(string disallowedOrigin)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = disallowedOrigin;
        context.Request.Headers["X-CSRF-Token"] = "matching-csrf-token";
        context.Request.Headers.Cookie = "kahoot_refresh_token=valid-refresh-token; kahoot_csrf_token=matching-csrf-token";

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        AssertForbidden(result, context);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Refresh_UsesCookieBeforeBodyToken()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = AllowedOrigin;
        context.Request.Headers["X-CSRF-Token"] = "csrf";
        context.Request.Headers.Cookie = "kahoot_refresh_token=cookie-refresh-tok; kahoot_csrf_token=csrf";

        RefreshResponse expectedResponse = new(Guid.NewGuid(), "user", "Host", "new.access.token", 900);
        _sender.RespondWith(Result<RefreshResult>.Success(new RefreshResult(expectedResponse, "rotated")));

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(new RefreshRequest("body-refresh-tok"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(_sender.Requests);
        RefreshCommand cmd = Assert.IsType<RefreshCommand>(_sender.Requests[0]);
        Assert.Equal("cookie-refresh-tok", cmd.RawRefreshToken);
    }

    [Fact]
    public async Task Refresh_BlankCookieDoesNotFallBackToBody()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers.Origin = AllowedOrigin;
        context.Request.Headers["X-CSRF-Token"] = "csrf";
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IRequestCookiesFeature>(
            new TestRequestCookiesFeature(new TestRequestCookieCollection(new Dictionary<string, string>
            {
                ["kahoot_refresh_token"] = "   ",
                ["kahoot_csrf_token"] = "csrf"
            })));

        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(new RefreshRequest("body-refresh-tok"), CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(AuthErrors.InvalidRefreshToken.Code, problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refresh_WithoutCookie_MissingOrBlankBodyToken_Returns401(string? bodyToken)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        context.Request.Headers["X-CSRF-Token"] = "csrf";

        AuthController controller = CreateController(context);
        RefreshRequest? request = bodyToken is null ? null : new RefreshRequest(bodyToken);
        IActionResult result = await controller.Refresh(request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task Logout_UsesCookieTokenOrNull()
    {
        // With cookie
        DefaultHttpContext contextWithCookie = HttpContextFactory.Create(path: "/api/auth/logout");
        contextWithCookie.Request.Headers.Origin = AllowedOrigin;
        contextWithCookie.Request.Headers["X-CSRF-Token"] = "csrf";
        contextWithCookie.Request.Headers.Cookie = "kahoot_refresh_token=cookie-tok; kahoot_csrf_token=csrf";
        _sender.RespondWith(Result.Success());

        AuthController controller1 = CreateController(contextWithCookie);
        IActionResult result1 = await controller1.Logout(CancellationToken.None);

        Assert.IsType<NoContentResult>(result1);
        LogoutCommand cmd1 = Assert.IsType<LogoutCommand>(_sender.Requests[0]);
        Assert.Equal("cookie-tok", cmd1.RawRefreshToken);

        // Without cookie
        DefaultHttpContext contextNoCookie = HttpContextFactory.Create(path: "/api/auth/logout");
        contextNoCookie.Request.Headers["X-CSRF-Token"] = "csrf";
        _sender.RespondWith(Result.Success());

        AuthController controller2 = CreateController(contextNoCookie);
        IActionResult result2 = await controller2.Logout(CancellationToken.None);

        Assert.IsType<NoContentResult>(result2);
        LogoutCommand cmd2 = Assert.IsType<LogoutCommand>(_sender.Requests[1]);
        Assert.Null(cmd2.RawRefreshToken);
    }

    [Fact]
    public async Task GuardFailurePrecedesMissingRefreshToken()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/refresh");
        // No CSRF header, no token
        AuthController controller = CreateController(context);
        IActionResult result = await controller.Refresh(null, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Empty(_sender.Requests);
    }

    private static void AssertForbidden(IActionResult result, HttpContext context)
    {
        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(AuthErrors.Forbidden.Code, problem.Extensions["code"]?.ToString());
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }
}
