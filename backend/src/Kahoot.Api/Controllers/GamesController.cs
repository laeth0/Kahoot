using Kahoot.Api.Common;
using Kahoot.Api.Contracts;
using Kahoot.Api.Realtime;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Games.CreateGame;
using Kahoot.Application.Games.EndGame;
using Kahoot.Application.Games.EndQuestion;
using Kahoot.Application.Games.GetHostGameState;
using Kahoot.Application.Games.GetLeaderboard;
using Kahoot.Application.Games.GetQuestionResults;
using Kahoot.Application.Games.JoinGame;
using Kahoot.Application.Games.RemoveParticipant;
using Kahoot.Application.Games.ShowLeaderboard;
using Kahoot.Application.Games.StartGame;
using Kahoot.Application.Games.StartNextQuestion;
using Kahoot.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kahoot.Api.Controllers;

[Route("api/games")]
public sealed class GamesController(ISender sender, GameNotifier notifier) : ApiControllerBase
{
    [HttpPost]
    [Authorize]
    [ProducesResponseType<CreateGameResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateGameRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateGameCommand(request.QuizId);
        return ToActionResult(
            await sender.Send(command, cancellationToken),
            game => CreatedAtAction(nameof(GetState), new { id = game.GameId }, game));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType<HostGameStateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetState(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetHostGameStateQuery(id);
        return ToActionResult(await sender.Send(query, cancellationToken), state => Ok(state));
    }

    [HttpGet("{id:guid}/questions/{questionId:guid}/results")]
    [Authorize]
    [ProducesResponseType<QuestionResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuestionResults(Guid id, Guid questionId, CancellationToken cancellationToken)
    {
        var query = new GetQuestionResultsQuery(id, questionId);
        return ToActionResult(await sender.Send(query, cancellationToken), results => Ok(results));
    }

    [HttpGet("{id:guid}/leaderboard")]
    [Authorize]
    [ProducesResponseType<LeaderboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeaderboard(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetLeaderboardQuery(id);
        return ToActionResult(await sender.Send(query, cancellationToken), leaderboard => Ok(leaderboard));
    }

    [HttpPost("{id:guid}/start")]
    [Authorize]
    [ProducesResponseType<QuestionStartedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        Result<QuestionStartedResponse> result = await sender.Send(new StartGameCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            await notifier.QuestionStartedAsync(id, result.Value);
        }

        return ToActionResult(result, question => Ok(question));
    }

    [HttpPost("{id:guid}/advance")]
    [Authorize]
    [ProducesResponseType<QuestionStartedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Advance(Guid id, CancellationToken cancellationToken)
    {
        Result<QuestionStartedResponse> result = await sender.Send(new StartNextQuestionCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            await notifier.QuestionStartedAsync(id, result.Value);
        }

        return ToActionResult(result, question => Ok(question));
    }

    [HttpPost("{id:guid}/end-question")]
    [Authorize]
    [ProducesResponseType<QuestionResultsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EndQuestion(Guid id, CancellationToken cancellationToken)
    {
        Result<QuestionResultsResponse> result = await sender.Send(new EndQuestionCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            await notifier.QuestionEndedAsync(id, result.Value);
        }

        return ToActionResult(result, results => Ok(results));
    }

    [HttpPost("{id:guid}/leaderboard")]
    [Authorize]
    [ProducesResponseType<LeaderboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ShowLeaderboard(Guid id, CancellationToken cancellationToken)
    {
        Result<LeaderboardResponse> result = await sender.Send(new ShowLeaderboardCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            await notifier.LeaderboardUpdatedAsync(id, result.Value);
        }

        return ToActionResult(result, leaderboard => Ok(leaderboard));
    }

    [HttpPost("{id:guid}/end")]
    [Authorize]
    [ProducesResponseType<LeaderboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> End(Guid id, CancellationToken cancellationToken)
    {
        Result<LeaderboardResponse> result = await sender.Send(new EndGameCommand(id), cancellationToken);

        if (result.IsSuccess)
        {
            await notifier.GameEndedAsync(id, result.Value);
        }

        return ToActionResult(result, leaderboard => Ok(leaderboard));
    }

    [HttpDelete("{id:guid}/participants/{participantId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveParticipant(Guid id, Guid participantId, CancellationToken cancellationToken)
    {
        Result result = await sender.Send(new RemoveParticipantCommand(id, participantId), cancellationToken);

        if (result.IsSuccess)
        {
            await notifier.ParticipantRemovedAsync(id, participantId);
        }

        return ToActionResult(result);
    }

    [HttpPost("join")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingExtensions.JoinPolicy)]
    [ProducesResponseType<JoinGameResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Join(JoinGameRequest request, CancellationToken cancellationToken)
    {
        Result<JoinGameResponse> result = await sender.Send(
            new JoinGameCommand(request.Pin, request.Nickname), cancellationToken);

        if (result.IsSuccess)
        {
            JoinGameResponse value = result.Value;
            await notifier.ParticipantJoinedAsync(
                value.GameId,
                new GameParticipantResponse(value.ParticipantId, value.Nickname, 0, null, false, false));
        }

        return ToActionResult(result, joinResult => Ok(joinResult));
    }
}
