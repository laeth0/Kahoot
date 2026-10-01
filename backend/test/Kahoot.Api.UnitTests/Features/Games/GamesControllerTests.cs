namespace Kahoot.Api.UnitTests.Features.Games;

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.Controllers;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.AdvanceQuestion;
using Kahoot.Application.Features.Games.CreateGame;
using Kahoot.Application.Features.Games.EndGame;
using Kahoot.Application.Features.Games.EndQuestion;
using Kahoot.Application.Features.Games.GetGame;
using Kahoot.Application.Features.Games.GetGameParticipants;
using Kahoot.Application.Features.Games.GetGameReport;
using Kahoot.Application.Features.Games.GetJoinInfo;
using Kahoot.Application.Features.Games.JoinGame;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Application.Features.Games.RemoveParticipant;
using Kahoot.Application.Features.Games.ShowLeaderboard;
using Kahoot.Application.Features.Games.StartGame;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class GamesControllerTests
{
    private readonly RecordingSender _sender = new();

    private GamesController CreateController(HttpContext httpContext)
    {
        return new GamesController(_sender)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Fact]
    public async Task CreateGame_Success_DispatchesCommandAndReturnsCreated()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/games");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        Guid quizId = Guid.NewGuid();
        Guid gameId = Guid.NewGuid();
        CreateGameRequest request = new(quizId);
        CreateGameResponse expectedResponse = new(
            gameId,
            "12345678",
            $"https://quiz.example.test/join/12345678",
            "History Trivia",
            "Lobby",
            1,
            10,
            DateTimeOffset.UtcNow);

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.CreateGame(request, cts.Token);

        CreatedResult created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/games/{gameId}", created.Location);
        Assert.Same(expectedResponse, created.Value);

        Assert.Single(_sender.Requests);
        CreateGameCommand command = Assert.IsType<CreateGameCommand>(_sender.Requests[0]);
        Assert.Equal(quizId, command.QuizId);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task CreateGame_Failure_ReturnsProblemDetails()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/games");
        GamesController controller = CreateController(context);

        CreateGameRequest request = new(Guid.NewGuid());
        _sender.RespondWith(Result.Failure<CreateGameResponse>(Error.NotFound("Quiz.NotFound", "Quiz was not found.")));

        IActionResult result = await controller.CreateGame(request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetGameById_Success_DispatchesQueryAndReturnsOk()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        GetGameResponse expectedResponse = new(
            gameId,
            "12345678",
            "Live Game",
            "Lobby",
            1,
            null,
            10,
            0,
            null,
            DateTimeOffset.UtcNow,
            null);

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.GetGameById(gameId, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        GetGameQuery query = Assert.IsType<GetGameQuery>(_sender.Requests[0]);
        Assert.Equal(gameId, query.GameId);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task GetGameById_Failure_ReturnsProblemDetails()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}");
        GamesController controller = CreateController(context);

        _sender.RespondWith(Result.Failure<GetGameResponse>(Error.NotFound("Game.NotFound", "Game not found.")));

        IActionResult result = await controller.GetGameById(gameId, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task StartGame_Success_DispatchesCommandAndReturnsOk()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/start");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        Guid commandId = Guid.NewGuid();
        GameControlRequest request = new(commandId, ExpectedStateVersion: 1);
        CurrentQuestionDto questionDto = new(
            Guid.NewGuid(),
            0,
            "First question?",
            null,
            20,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddSeconds(20),
            10,
            new List<QuestionChoiceDto>());
        StartGameResponse expectedResponse = new(gameId, "QuestionActive", 2, questionDto);

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.StartGame(gameId, request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        StartGameCommand command = Assert.IsType<StartGameCommand>(_sender.Requests[0]);
        Assert.Equal(gameId, command.GameId);
        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(1, command.ExpectedStateVersion);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task StartGame_Failure_ReturnsProblemDetails()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/start");
        GamesController controller = CreateController(context);

        GameControlRequest request = new(Guid.NewGuid(), 1);
        _sender.RespondWith(Result.Failure<StartGameResponse>(Error.Conflict("Game.InvalidStateTransition", "Game cannot be started from current state.")));

        IActionResult result = await controller.StartGame(gameId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task EndQuestion_Success_DispatchesCommandAndReturnsOk()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/end-question");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        Guid commandId = Guid.NewGuid();
        GameControlRequest request = new(commandId, ExpectedStateVersion: 2);
        EndQuestionResponse expectedResponse = new(
            gameId,
            "QuestionResults",
            3,
            Guid.NewGuid(),
            0,
            15,
            DateTimeOffset.UtcNow,
            new List<QuestionChoiceResultDto>());

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.EndQuestion(gameId, request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        EndQuestionCommand command = Assert.IsType<EndQuestionCommand>(_sender.Requests[0]);
        Assert.Equal(gameId, command.GameId);
        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(2, command.ExpectedStateVersion);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task ShowLeaderboard_Success_DispatchesCommandAndReturnsOk()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/show-leaderboard");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        Guid commandId = Guid.NewGuid();
        GameControlRequest request = new(commandId, ExpectedStateVersion: 3);
        ShowLeaderboardResponse expectedResponse = new(gameId, "Leaderboard", 4, new List<LeaderboardParticipantDto>());

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.ShowLeaderboard(gameId, request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        ShowLeaderboardCommand command = Assert.IsType<ShowLeaderboardCommand>(_sender.Requests[0]);
        Assert.Equal(gameId, command.GameId);
        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(3, command.ExpectedStateVersion);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task AdvanceQuestion_Success_DispatchesCommandAndReturnsOk()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/advance");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        Guid commandId = Guid.NewGuid();
        GameControlRequest request = new(commandId, ExpectedStateVersion: 4);
        CurrentQuestionDto questionDto = new(
            Guid.NewGuid(),
            1,
            "Second question?",
            null,
            20,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddSeconds(20),
            10,
            new List<QuestionChoiceDto>());
        AdvanceQuestionResponse expectedResponse = new(gameId, "QuestionActive", 5, questionDto);

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.AdvanceQuestion(gameId, request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        AdvanceQuestionCommand command = Assert.IsType<AdvanceQuestionCommand>(_sender.Requests[0]);
        Assert.Equal(gameId, command.GameId);
        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(4, command.ExpectedStateVersion);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task EndGame_Success_DispatchesCommandAndReturnsOk()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/end");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        Guid commandId = Guid.NewGuid();
        GameControlRequest request = new(commandId, ExpectedStateVersion: 5);
        EndGameResponse expectedResponse = new(gameId, "Finished", 6, DateTimeOffset.UtcNow, new List<PodiumParticipantDto>());

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.EndGame(gameId, request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        EndGameCommand command = Assert.IsType<EndGameCommand>(_sender.Requests[0]);
        Assert.Equal(gameId, command.GameId);
        Assert.Equal(commandId, command.CommandId);
        Assert.Equal(5, command.ExpectedStateVersion);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task GetGameReport_Success_DispatchesQueryAndReturnsOk()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/report");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        GetGameReportResponse expectedResponse = new(
            gameId,
            "History Trivia",
            "Finished",
            10,
            25,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            DateTimeOffset.UtcNow,
            new List<QuestionReportDto>(),
            new List<LeaderboardParticipantDto>());

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.GetGameReport(gameId, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        GetGameReportQuery query = Assert.IsType<GetGameReportQuery>(_sender.Requests[0]);
        Assert.Equal(gameId, query.GameId);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task JoinGame_WithRemoteIp_DispatchesCommandWithIpAndReturnsOk()
    {
        IPAddress clientIp = IPAddress.Parse("203.0.113.195");
        DefaultHttpContext context = HttpContextFactory.Create(remoteIpAddress: clientIp, path: "/api/games/join");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        Guid joinOpId = Guid.NewGuid();
        JoinGameRequest request = new("12345678", "SpeedyFox", joinOpId);
        JoinGameResponse expectedResponse = new(Guid.NewGuid(), "pst_token123", Guid.NewGuid(), "SpeedyFox", "Trivia", 1);

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.JoinGame(request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        JoinGameCommand command = Assert.IsType<JoinGameCommand>(_sender.Requests[0]);
        Assert.Equal("12345678", command.Pin);
        Assert.Equal("SpeedyFox", command.Nickname);
        Assert.Equal(joinOpId, command.JoinOperationId);
        Assert.Equal("203.0.113.195", command.IpAddress);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task JoinGame_WithoutRemoteIp_DispatchesCommandWithUnknownIp()
    {
        DefaultHttpContext context = HttpContextFactory.Create(remoteIpAddress: null, path: "/api/games/join");
        GamesController controller = CreateController(context);

        JoinGameRequest request = new("12345678", "SpeedyFox", Guid.NewGuid());
        JoinGameResponse expectedResponse = new(Guid.NewGuid(), "pst_token123", Guid.NewGuid(), "SpeedyFox", "Trivia", 1);
        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.JoinGame(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        JoinGameCommand command = Assert.IsType<JoinGameCommand>(_sender.Requests[0]);
        Assert.Equal("unknown", command.IpAddress);
    }

    [Fact]
    public async Task GetJoinInfo_Success_DispatchesQueryAndReturnsOk()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/games/join/12345678");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        GetJoinInfoResponse expectedResponse = new(Guid.NewGuid(), "Geography Bee", "Lobby", 12, 500, false);
        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.GetJoinInfo("12345678", cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        GetJoinInfoQuery query = Assert.IsType<GetJoinInfoQuery>(_sender.Requests[0]);
        Assert.Equal("12345678", query.Pin);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task RemoveParticipant_Success_DispatchesCommandAndReturnsNoContent()
    {
        Guid gameId = Guid.NewGuid();
        Guid participantId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/participants/{participantId}");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        _sender.RespondWith(Result.Success());

        IActionResult result = await controller.RemoveParticipant(gameId, participantId, cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        RemoveParticipantCommand command = Assert.IsType<RemoveParticipantCommand>(_sender.Requests[0]);
        Assert.Equal(gameId, command.GameId);
        Assert.Equal(participantId, command.ParticipantId);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task GetGameParticipants_WithDefaults_DispatchesQueryAndReturnsOk()
    {
        Guid gameId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/games/{gameId}/participants");
        GamesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        GetGameParticipantsResponse expectedResponse = new(gameId, 1, 5, new List<ParticipantDto>(), null);
        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.GetGameParticipants(gameId, includeRemoved: false, limit: null, cursor: null, cancellationToken: cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        GetGameParticipantsQuery query = Assert.IsType<GetGameParticipantsQuery>(_sender.Requests[0]);
        Assert.Equal(gameId, query.GameId);
        Assert.False(query.IncludeRemoved);
        Assert.Null(query.Limit);
        Assert.Null(query.Cursor);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }
}
