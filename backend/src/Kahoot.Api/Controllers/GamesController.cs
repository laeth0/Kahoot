namespace Kahoot.Api.Controllers;

using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Persistence;
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
using Kahoot.Application.Features.Games.SubmitAnswer;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Live Game Session Management Controller - Orchestrates authoritative 6-state game lifecycle, host command execution, anonymous participant joins, and post-game reporting.
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

    // Create Game Session Endpoint - Freezes quiz questions into immutable snapshots under RepeatableRead isolation and assigns random 8-digit PIN.
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

    // Get Game Details Endpoint - Retrieves host-scoped game session metadata.
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

    // Start Game Session Endpoint - Transitions game from Lobby to QuestionActive for the first question.
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

    // End Question Endpoint - Manually expires question timer and materializes choice selection counts.
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

    // Show Leaderboard Endpoint - Transitions game to Leaderboard state and computes current participant ranks.
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

    // Advance Question Endpoint - Advances game to next question snapshot or final leaderboard if no questions remain.
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

    // End Game Session Endpoint - Finalizes live game, releases PIN, and transitions game to Finished status.
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

    // Get Game Report Endpoint - Materializes immutable post-game analytics report including question statistics and participant rankings.
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

    // Anonymous Participant Join Endpoint - Validates PIN, claims seat number, and issues participant session token.
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

    // Get Join Info Endpoint - Queries public game title and joinability status for a PIN.
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

    // Remove Participant Endpoint - Ejects participant from active game and publishes eviction notice.
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

    // Get Game Participants Endpoint - Returns keyset-paginated roster of game participants.
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

    // Participant Answer Submission REST Endpoint (PLAY-ANS-001) - Ingests choice selection for active question via player session token authentication.
    [HttpPost("{id:guid}/answers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SubmitAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SubmitAnswer(
        [FromRoute] Guid id,
        [FromBody] SubmitAnswerRequest request,
        [FromServices] IAppDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        string? rawToken = Request.Headers["X-Session-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            string? authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                rawToken = authHeader["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(rawToken) || rawToken.Length != 68 || !rawToken.StartsWith("pst_", StringComparison.Ordinal))
        {
            return Problem(GameErrors.InvalidSessionToken);
        }

        byte[] tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        ParticipantSessionToken? sessionToken = await dbContext.ParticipantSessionTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.GameId == id && t.TokenHash == tokenHash && t.RevokedAt == null, cancellationToken);

        if (sessionToken is null)
        {
            return Problem(GameErrors.InvalidSessionToken);
        }

        SubmitAnswerCommand command = new SubmitAnswerCommand(
            id,
            sessionToken.ParticipantId,
            request.QuestionId,
            request.ChoiceIds,
            string.Empty,
            tokenHash,
            null);

        Result<SubmitAnswerResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }
}
