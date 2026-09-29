namespace Kahoot.Api.Controllers;

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
using Kahoot.Application.Features.Games.RemoveParticipant;
using Kahoot.Application.Features.Games.ShowLeaderboard;
using Kahoot.Application.Features.Games.StartGame;
using Kahoot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/games")]
[Authorize(Roles = nameof(UserRole.Host))]
public sealed class GamesController : ApiController
{
    private readonly ISender _sender;

    public GamesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateGameResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateGame(
        [FromBody] CreateGameRequest request,
        CancellationToken cancellationToken = default)
    {
        CreateGameCommand command = new CreateGameCommand(request.QuizId);
        Result<CreateGameResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Created($"/api/games/{result.Value.GameId}", result.Value);
        }

        return Problem(result.Error);
    }

    [HttpGet("{id:guid}")]
    [ActionName(nameof(GetGameById))]
    [ProducesResponseType(typeof(GetGameResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        GetGameQuery query = new GetGameQuery(id);
        Result<GetGameResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(StartGameResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartGame(
        [FromRoute] Guid id,
        [FromBody] GameControlRequest request,
        CancellationToken cancellationToken = default)
    {
        StartGameCommand command = new StartGameCommand(id, request.CommandId, request.ExpectedStateVersion);
        Result<StartGameResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost("{id:guid}/end-question")]
    [ProducesResponseType(typeof(EndQuestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EndQuestion(
        [FromRoute] Guid id,
        [FromBody] GameControlRequest request,
        CancellationToken cancellationToken = default)
    {
        EndQuestionCommand command = new EndQuestionCommand(id, request.CommandId, request.ExpectedStateVersion);
        Result<EndQuestionResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost("{id:guid}/show-leaderboard")]
    [ProducesResponseType(typeof(ShowLeaderboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ShowLeaderboard(
        [FromRoute] Guid id,
        [FromBody] GameControlRequest request,
        CancellationToken cancellationToken = default)
    {
        ShowLeaderboardCommand command = new ShowLeaderboardCommand(id, request.CommandId, request.ExpectedStateVersion);
        Result<ShowLeaderboardResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost("{id:guid}/advance")]
    [ProducesResponseType(typeof(AdvanceQuestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdvanceQuestion(
        [FromRoute] Guid id,
        [FromBody] GameControlRequest request,
        CancellationToken cancellationToken = default)
    {
        AdvanceQuestionCommand command = new AdvanceQuestionCommand(id, request.CommandId, request.ExpectedStateVersion);
        Result<AdvanceQuestionResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost("{id:guid}/end")]
    [ProducesResponseType(typeof(EndGameResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EndGame(
        [FromRoute] Guid id,
        [FromBody] GameControlRequest request,
        CancellationToken cancellationToken = default)
    {
        EndGameCommand command = new EndGameCommand(id, request.CommandId, request.ExpectedStateVersion);
        Result<EndGameResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpGet("{id:guid}/report")]
    [ProducesResponseType(typeof(GetGameReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetGameReport(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        GetGameReportQuery query = new GetGameReportQuery(id);
        Result<GetGameReportResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost("join")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(JoinGameResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> JoinGame(
        [FromBody] JoinGameRequest request,
        CancellationToken cancellationToken = default)
    {
        string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        JoinGameCommand command = new JoinGameCommand(
            request.Pin,
            request.Nickname,
            request.JoinOperationId,
            ipAddress);

        Result<JoinGameResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpGet("join/{pin}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(GetJoinInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetJoinInfo(
        [FromRoute] string pin,
        CancellationToken cancellationToken = default)
    {
        GetJoinInfoQuery query = new GetJoinInfoQuery(pin);
        Result<GetJoinInfoResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpDelete("{id:guid}/participants/{participantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveParticipant(
        [FromRoute] Guid id,
        [FromRoute] Guid participantId,
        CancellationToken cancellationToken = default)
    {
        RemoveParticipantCommand command = new RemoveParticipantCommand(id, participantId);
        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return Problem(result.Error);
    }

    [HttpGet("{id:guid}/participants")]
    [ProducesResponseType(typeof(GetGameParticipantsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGameParticipants(
        [FromRoute] Guid id,
        [FromQuery] bool includeRemoved = false,
        [FromQuery] int? limit = null,
        [FromQuery] int? cursor = null,
        CancellationToken cancellationToken = default)
    {
        GetGameParticipantsQuery query = new GetGameParticipantsQuery(id, includeRemoved, limit, cursor);
        Result<GetGameParticipantsResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }
}
