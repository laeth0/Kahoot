namespace Kahoot.Api.UnitTests.Features.Games;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.Controllers;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.SubmitAnswer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class SubmitAnswerTokenGuardTests
{
    private readonly RecordingSender _sender = new();
    private readonly FailOnUseDbContext _failDbContext = new();

    private GamesController CreateController(HttpContext httpContext)
    {
        return new GamesController(_sender)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    private static SubmitAnswerRequest CreateSampleRequest()
    {
        return new SubmitAnswerRequest(
            QuestionId: Guid.NewGuid(),
            ChoiceIds: new List<Guid> { Guid.NewGuid() });
    }

    [Fact]
    public async Task SubmitAnswer_MissingTokenHeaders_Returns401BeforeDbContext()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{Guid.NewGuid()}/answers");
        GamesController controller = CreateController(context);

        IActionResult result = await controller.SubmitAnswer(Guid.NewGuid(), CreateSampleRequest(), _failDbContext, CancellationToken.None);

        AssertInvalidSessionToken(result);
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SubmitAnswer_EmptyOrWhitespaceSessionToken_WithoutBearer_Returns401(string emptyOrWhitespaceToken)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{Guid.NewGuid()}/answers");
        context.Request.Headers["X-Session-Token"] = emptyOrWhitespaceToken;
        GamesController controller = CreateController(context);

        IActionResult result = await controller.SubmitAnswer(Guid.NewGuid(), CreateSampleRequest(), _failDbContext, CancellationToken.None);

        AssertInvalidSessionToken(result);
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData(67)]
    [InlineData(69)]
    public async Task SubmitAnswer_WrongLengthToken_Returns401(int totalLength)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{Guid.NewGuid()}/answers");
        string token = "pst_" + new string('a', totalLength - 4);
        context.Request.Headers["X-Session-Token"] = token;
        GamesController controller = CreateController(context);

        IActionResult result = await controller.SubmitAnswer(Guid.NewGuid(), CreateSampleRequest(), _failDbContext, CancellationToken.None);

        AssertInvalidSessionToken(result);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task SubmitAnswer_WrongCasePrefix_Returns401()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{Guid.NewGuid()}/answers");
        string uppercasePrefixToken = "PST_" + new string('a', 64);
        Assert.Equal(68, uppercasePrefixToken.Length);
        context.Request.Headers["X-Session-Token"] = uppercasePrefixToken;
        GamesController controller = CreateController(context);

        IActionResult result = await controller.SubmitAnswer(Guid.NewGuid(), CreateSampleRequest(), _failDbContext, CancellationToken.None);

        AssertInvalidSessionToken(result);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task SubmitAnswer_MissingPrefix_Returns401()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{Guid.NewGuid()}/answers");
        string noPrefixToken = "xyz_" + new string('b', 64);
        Assert.Equal(68, noPrefixToken.Length);
        context.Request.Headers["X-Session-Token"] = noPrefixToken;
        GamesController controller = CreateController(context);

        IActionResult result = await controller.SubmitAnswer(Guid.NewGuid(), CreateSampleRequest(), _failDbContext, CancellationToken.None);

        AssertInvalidSessionToken(result);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task SubmitAnswer_MalformedSessionToken_TakesPrecedenceOverValidBearer_AndRejectsBeforeDb()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{Guid.NewGuid()}/answers");
        context.Request.Headers["X-Session-Token"] = "malformed_token";
        // Syntactically valid Bearer token that would reach DB if evaluated:
        string validSyntaxToken = "pst_" + new string('c', 64);
        context.Request.Headers.Authorization = $"Bearer {validSyntaxToken}";

        GamesController controller = CreateController(context);

        // If it accessed DB, _failDbContext would throw InvalidOperationException.
        // It should reject immediately with 401 because X-Session-Token has precedence.
        IActionResult result = await controller.SubmitAnswer(Guid.NewGuid(), CreateSampleRequest(), _failDbContext, CancellationToken.None);

        AssertInvalidSessionToken(result);
        Assert.Empty(_sender.Requests);
    }

    [Theory]
    [InlineData("Basic dXNlcjpwYXNz")]
    [InlineData("Bearer ")]
    [InlineData("Bearer    ")]
    [InlineData("Bearer pst_short")]
    [InlineData("bearer pst_short")]
    public async Task SubmitAnswer_InvalidAuthorizationHeader_Returns401BeforeDb(string authHeader)
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{Guid.NewGuid()}/answers");
        context.Request.Headers.Authorization = authHeader;
        GamesController controller = CreateController(context);

        IActionResult result = await controller.SubmitAnswer(Guid.NewGuid(), CreateSampleRequest(), _failDbContext, CancellationToken.None);

        AssertInvalidSessionToken(result);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task SubmitAnswer_SessionTokenNotTrimmed_PaddedTokenRejectsBeforeDb()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{Guid.NewGuid()}/answers");
        // Token has leading spaces and length > 68
        string paddedToken = "  pst_" + new string('a', 64);
        context.Request.Headers["X-Session-Token"] = paddedToken;
        GamesController controller = CreateController(context);

        IActionResult result = await controller.SubmitAnswer(Guid.NewGuid(), CreateSampleRequest(), _failDbContext, CancellationToken.None);

        AssertInvalidSessionToken(result);
        Assert.Empty(_sender.Requests);
    }

    private static void AssertInvalidSessionToken(IActionResult result)
    {
        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(GameErrors.InvalidSessionToken.Code, problem.Extensions["code"]?.ToString());
    }
}
