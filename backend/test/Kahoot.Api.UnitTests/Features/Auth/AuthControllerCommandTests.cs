namespace Kahoot.Api.UnitTests.Features.Auth;

using System;
using System.Net;
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
using Kahoot.Application.Features.Auth.Register;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class AuthControllerCommandTests
{
    private readonly RecordingSender _sender = new();
    private readonly IOptions<RefreshTokenOptions> _refreshOptions = Options.Create(new RefreshTokenOptions { LifetimeDays = 7 });
    private readonly IOptions<CorsOptions> _corsOptions = Options.Create(new CorsOptions
    {
        AllowedOrigins = new[] { "https://quiz.example.test" }
    });

    private AuthController CreateController(HttpContext httpContext)
    {
        return new AuthController(_sender, _refreshOptions, _corsOptions)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Fact]
    public async Task Register_Success_DispatchesCommandAndReturnsCreated()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/register");
        AuthController controller = CreateController(context);
        CancellationTokenSource cts = new();

        RegisterRequest request = new("testuser", "P@ssword123!");
        RegisterResponse expectedResponse = new(Guid.NewGuid(), "testuser");
        _sender.RespondWith(Result<RegisterResponse>.Success(expectedResponse));

        IActionResult result = await controller.Register(request, cts.Token);

        CreatedResult createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        Assert.Equal(string.Empty, createdResult.Location);
        Assert.Same(expectedResponse, createdResult.Value);

        Assert.Single(_sender.Requests);
        RegisterCommand dispatched = Assert.IsType<RegisterCommand>(_sender.Requests[0]);
        Assert.Equal(request.Username, dispatched.Username);
        Assert.Equal(request.Password, dispatched.Password);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task Register_Failure_ReturnsProblemDetailsWithoutCookieSideEffects()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/register");
        AuthController controller = CreateController(context);

        RegisterRequest request = new("duplicateuser", "P@ssword123!");
        Error error = Error.Conflict("User.UsernameExists", "Username is already taken.");
        _sender.RespondWith(Result.Failure<RegisterResponse>(error));

        IActionResult result = await controller.Register(request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("User.UsernameExists", problem.Extensions["code"]?.ToString());

        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task Login_WithRemoteIp_DispatchesCommandWithIpAndReturnsOkResponse()
    {
        IPAddress clientIp = IPAddress.Parse("192.168.1.50");
        DefaultHttpContext context = HttpContextFactory.Create(remoteIpAddress: clientIp, path: "/api/auth/login");
        AuthController controller = CreateController(context);
        CancellationTokenSource cts = new();

        LoginRequest request = new("hostuser", "P@ssword123!");
        LoginResponse publicResponse = new(Guid.NewGuid(), "hostuser", "Host", "access.jwt.token", 900);
        LoginResult loginResult = new(publicResponse, "raw-refresh-token-secret");
        _sender.RespondWith(Result.Success(loginResult));

        IActionResult result = await controller.Login(request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(publicResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        LoginCommand dispatched = Assert.IsType<LoginCommand>(_sender.Requests[0]);
        Assert.Equal(request.Username, dispatched.Username);
        Assert.Equal(request.Password, dispatched.Password);
        Assert.Equal("192.168.1.50", dispatched.IpAddress);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task Login_WithoutRemoteIp_DispatchesCommandWithUnknownIp()
    {
        DefaultHttpContext context = HttpContextFactory.Create(remoteIpAddress: null, path: "/api/auth/login");
        AuthController controller = CreateController(context);

        LoginRequest request = new("hostuser", "P@ssword123!");
        LoginResponse publicResponse = new(Guid.NewGuid(), "hostuser", "Host", "access.jwt.token", 900);
        LoginResult loginResult = new(publicResponse, "raw-refresh-token-secret");
        _sender.RespondWith(Result.Success(loginResult));

        IActionResult result = await controller.Login(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        LoginCommand dispatched = Assert.IsType<LoginCommand>(_sender.Requests[0]);
        Assert.Equal("unknown", dispatched.IpAddress);
    }

    [Fact]
    public async Task Login_Failure_ReturnsProblemDetailsWithoutCookieSideEffects()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/login");
        AuthController controller = CreateController(context);

        LoginRequest request = new("hostuser", "WrongPassword!");
        _sender.RespondWith(Result.Failure<LoginResult>(AuthErrors.InvalidCredentials));

        IActionResult result = await controller.Login(request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, problem.Extensions["code"]?.ToString());

        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task LogoutAll_Success_DispatchesCommandReturnsNoContentAndClearsCookies()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/logout-all");
        AuthController controller = CreateController(context);
        CancellationTokenSource cts = new();

        _sender.RespondWith(Result.Success());

        IActionResult result = await controller.LogoutAll(cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        Assert.IsType<LogoutAllCommand>(_sender.Requests[0]);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);

        Assert.True(context.Response.Headers.SetCookie.Count > 0);
    }

    [Fact]
    public async Task LogoutAll_Failure_ReturnsProblemDetailsWithoutClearingCookies()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/logout-all");
        AuthController controller = CreateController(context);

        _sender.RespondWith(Result.Failure(AuthErrors.Forbidden));

        IActionResult result = await controller.LogoutAll(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task ChangePassword_Success_DispatchesCommandReturnsNoContentAndClearsCookies()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/change-password");
        AuthController controller = CreateController(context);
        CancellationTokenSource cts = new();

        ChangePasswordRequest request = new("OldP@ss123!", "NewP@ss456!");
        _sender.RespondWith(Result.Success());

        IActionResult result = await controller.ChangePassword(request, cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        ChangePasswordCommand dispatched = Assert.IsType<ChangePasswordCommand>(_sender.Requests[0]);
        Assert.Equal(request.CurrentPassword, dispatched.CurrentPassword);
        Assert.Equal(request.NewPassword, dispatched.NewPassword);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);

        Assert.True(context.Response.Headers.SetCookie.Count > 0);
    }

    [Fact]
    public async Task ChangePassword_Failure_ReturnsProblemDetailsWithoutClearingCookies()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/auth/change-password");
        AuthController controller = CreateController(context);

        ChangePasswordRequest request = new("WrongOldP@ss!", "NewP@ss456!");
        _sender.RespondWith(Result.Failure(AuthErrors.InvalidCredentials));

        IActionResult result = await controller.ChangePassword(request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);

        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }
}
