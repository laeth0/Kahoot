using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.CreateQuiz;
using Kahoot.Application.Features.Quizzes.DeleteQuiz;
using Kahoot.Application.Features.Quizzes.GetQuizById;
using Kahoot.Application.Features.Quizzes.ListQuizzes;
using Kahoot.Application.Features.Quizzes.PublishQuiz;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Application.Features.Quizzes.Questions.AddQuestion;
using Kahoot.Application.Features.Quizzes.Questions.DeleteQuestion;
using Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;
using Kahoot.Application.Features.Quizzes.ReorderQuestions;
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

    [HttpDelete("{quizId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteQuiz(
        [FromRoute] Guid quizId,
        CancellationToken cancellationToken = default)
    {
        DeleteQuizCommand command = new DeleteQuizCommand(quizId);
        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return Problem(result.Error);
    }

    [HttpPost("{quizId:guid}/questions")]
    [ProducesResponseType(typeof(QuestionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddQuestion(
        [FromRoute] Guid quizId,
        [FromBody] AddQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        AddQuestionCommand command = new AddQuestionCommand(
            quizId,
            request.Text,
            request.ImageId,
            request.DurationSeconds,
            request.BasePoints,
            request.Choices);

        Result<QuestionResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Created($"/api/quizzes/{quizId}/questions/{result.Value.Id}", result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPut("{quizId:guid}/questions/{questionId:guid}")]
    [ProducesResponseType(typeof(QuestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateQuestion(
        [FromRoute] Guid quizId,
        [FromRoute] Guid questionId,
        [FromBody] UpdateQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        UpdateQuestionCommand command = new UpdateQuestionCommand(
            quizId,
            questionId,
            request.Text,
            request.ImageId,
            request.DurationSeconds,
            request.BasePoints,
            request.Choices);

        Result<QuestionResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpDelete("{quizId:guid}/questions/{questionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteQuestion(
        [FromRoute] Guid quizId,
        [FromRoute] Guid questionId,
        CancellationToken cancellationToken = default)
    {
        DeleteQuestionCommand command = new DeleteQuestionCommand(quizId, questionId);
        Result result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return Problem(result.Error);
    }

    [HttpPost("{quizId:guid}/reorder")]
    [ProducesResponseType(typeof(ReorderQuestionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReorderQuestions(
        [FromRoute] Guid quizId,
        [FromBody] ReorderQuestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        ReorderQuestionsCommand command = new ReorderQuestionsCommand(quizId, request.QuestionIds);
        Result<ReorderQuestionsResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }

    [HttpPost("{quizId:guid}/publish")]
    [ProducesResponseType(typeof(PublishQuizResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PublishQuiz(
        [FromRoute] Guid quizId,
        CancellationToken cancellationToken = default)
    {
        PublishQuizCommand command = new PublishQuizCommand(quizId);
        Result<PublishQuizResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Problem(result.Error);
    }
}
