using Kahoot.Api.Common;
using Kahoot.Api.Contracts;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Application.Quizzes.CreateQuiz;
using Kahoot.Application.Quizzes.DeleteQuiz;
using Kahoot.Application.Quizzes.GetQuiz;
using Kahoot.Application.Quizzes.ListQuizzes;
using Kahoot.Application.Quizzes.PublishQuiz;
using Kahoot.Application.Quizzes.Questions.AddQuestion;
using Kahoot.Application.Quizzes.Questions.DeleteQuestion;
using Kahoot.Application.Quizzes.Questions.ReorderQuestions;
using Kahoot.Application.Quizzes.Questions.UpdateQuestion;
using Kahoot.Application.Quizzes.UpdateQuiz;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Controllers;

[Authorize]
[Route("api/quizzes")]
public sealed class QuizzesController(ISender sender) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<QuizSummaryResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var query = new ListQuizzesQuery();
        return ToActionResult(await sender.Send(query, cancellationToken), quizzes => Ok(quizzes));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<QuizDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetQuizQuery(id);
        return ToActionResult(await sender.Send(query, cancellationToken), quiz => Ok(quiz));
    }

    [HttpPost]
    [ProducesResponseType<CreatedIdResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateQuizRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateQuizCommand(request.Title, request.Description);
        return ToActionResult(
            await sender.Send(command, cancellationToken),
            id => CreatedAtAction(nameof(Get), new { id }, new CreatedIdResponse(id)));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateQuizRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateQuizCommand(id, request.Title, request.Description);
        return ToActionResult(await sender.Send(command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteQuizCommand(id);
        return ToActionResult(await sender.Send(command, cancellationToken));
    }

    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var command = new PublishQuizCommand(id);
        return ToActionResult(await sender.Send(command, cancellationToken));
    }

    [HttpPost("{id:guid}/questions")]
    [ProducesResponseType<CreatedIdResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddQuestion(
        Guid id,
        SaveQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddQuestionCommand(
            id,
            request.Text,
            request.ImageUrl,
            request.TimeLimitSeconds,
            request.Points,
            request.Choices);

        return ToActionResult(
            await sender.Send(command, cancellationToken),
            questionId => CreatedAtAction(nameof(Get), new { id }, new CreatedIdResponse(questionId)));
    }

    [HttpPut("{id:guid}/questions/{questionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateQuestion(
        Guid id,
        Guid questionId,
        SaveQuestionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateQuestionCommand(
            id,
            questionId,
            request.Text,
            request.ImageUrl,
            request.TimeLimitSeconds,
            request.Points,
            request.Choices);

        return ToActionResult(await sender.Send(command, cancellationToken));
    }

    [HttpDelete("{id:guid}/questions/{questionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteQuestion(Guid id, Guid questionId, CancellationToken cancellationToken)
    {
        var command = new DeleteQuestionCommand(id, questionId);
        return ToActionResult(await sender.Send(command, cancellationToken));
    }

    [HttpPut("{id:guid}/questions/order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReorderQuestions(
        Guid id,
        ReorderQuestionsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ReorderQuestionsCommand(id, request.OrderedQuestionIds);
        return ToActionResult(await sender.Send(command, cancellationToken));
    }
}
