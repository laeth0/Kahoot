using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Quizzes.CreateQuiz;
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
            return Created($"/api/quizzes/{result.Value.Id}", result.Value);
        }

        return Problem(result.Error);
    }
}
