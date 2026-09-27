using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.CreateQuiz;
using Kahoot.Application.Features.Quizzes.GetQuizById;
using Kahoot.Application.Features.Quizzes.ListQuizzes;
using Kahoot.Application.Features.Quizzes.UpdateQuiz;
using Kahoot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Controllers;

[ApiController]
[Route("api/quizzes")]
[Authorize(Roles = nameof(UserRole.Host))]
public sealed class QuizzesController : ApiController
{
    private readonly ISender _sender;

    public QuizzesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost]
    [ProducesResponseType(typeof(QuizSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateQuiz(
        [FromBody] CreateQuizRequest request,
        CancellationToken cancellationToken = default)
    {
        CreateQuizCommand command = new CreateQuizCommand(request.Title, request.Description);
        Result<QuizSummaryResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return CreatedAtAction(nameof(GetQuizById), new { quizId = result.Value.Id }, result.Value);
        }

        return Problem(result.Error);
    }

    [HttpGet]
    [ProducesResponseType(typeof(ListQuizzesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListQuizzes(
        [FromQuery] string? cursor = null,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        ListQuizzesQuery query = new ListQuizzesQuery(cursor, pageSize);
        Result<ListQuizzesResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpGet("{quizId:guid}")]
    [ProducesResponseType(typeof(QuizDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuizById(
        [FromRoute] Guid quizId,
        CancellationToken cancellationToken = default)
    {
        GetQuizByIdQuery query = new GetQuizByIdQuery(quizId);
        Result<QuizDetailsResponse> result = await _sender.Send(query, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPut("{quizId:guid}")]
    [ProducesResponseType(typeof(QuizSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateQuiz(
        [FromRoute] Guid quizId,
        [FromBody] UpdateQuizRequest request,
        CancellationToken cancellationToken = default)
    {
        UpdateQuizCommand command = new UpdateQuizCommand(quizId, request.Title, request.Description);
        Result<QuizSummaryResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }
}
