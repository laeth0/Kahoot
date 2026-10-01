namespace Kahoot.Api.UnitTests.Features.Quizzes;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.Controllers;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Quizzes;
using Kahoot.Application.Features.Quizzes.CreateQuiz;
using Kahoot.Application.Features.Quizzes.DeleteQuiz;
using Kahoot.Application.Features.Quizzes.GetQuizById;
using Kahoot.Application.Features.Quizzes.ListQuizzes;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Application.Features.Quizzes.Questions.AddQuestion;
using Kahoot.Application.Features.Quizzes.Questions.DeleteQuestion;
using Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;
using Kahoot.Application.Features.Quizzes.ReorderQuestions;
using Kahoot.Application.Features.Quizzes.UpdateQuiz;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class QuizzesControllerTests
{
    private readonly RecordingSender _sender = new();

    private QuizzesController CreateController(HttpContext httpContext)
    {
        return new QuizzesController(_sender)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Fact]
    public async Task CreateQuiz_Success_DispatchesCommandAndReturnsCreatedAtAction()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/quizzes");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        CreateQuizRequest request = new("World History", "A quiz about world history events.");
        QuizSummaryResponse expectedResponse = new(
            Guid.NewGuid(),
            request.Title,
            request.Description,
            1,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.CreateQuiz(request, cts.Token);

        CreatedAtActionResult created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(QuizzesController.GetQuizById), created.ActionName);
        Assert.Equal(expectedResponse.Id, created.RouteValues?["quizId"]);
        Assert.Same(expectedResponse, created.Value);

        Assert.Single(_sender.Requests);
        CreateQuizCommand command = Assert.IsType<CreateQuizCommand>(_sender.Requests[0]);
        Assert.Equal(request.Title, command.Title);
        Assert.Equal(request.Description, command.Description);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task CreateQuiz_Failure_ReturnsProblemDetails()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/quizzes");
        QuizzesController controller = CreateController(context);

        CreateQuizRequest request = new("Invalid Quiz", null);
        Error error = Error.Validation("Quiz.TitleRequired", "Title is required.");
        _sender.RespondWith(Result.Failure<QuizSummaryResponse>(error));

        IActionResult result = await controller.CreateQuiz(request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Quiz.TitleRequired", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task ListQuizzes_WithDefaults_DispatchesQueryAndReturnsOk()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/quizzes");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        ListQuizzesResponse expectedResponse = new(new List<QuizSummaryResponse>(), null, false);
        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.ListQuizzes(cursor: null, pageSize: 50, cancellationToken: cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        ListQuizzesQuery query = Assert.IsType<ListQuizzesQuery>(_sender.Requests[0]);
        Assert.Null(query.Cursor);
        Assert.Equal(50, query.PageSize);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task ListQuizzes_Failure_ReturnsProblemDetails()
    {
        DefaultHttpContext context = HttpContextFactory.Create(path: "/api/quizzes");
        QuizzesController controller = CreateController(context);

        _sender.RespondWith(Result.Failure<ListQuizzesResponse>(Error.Validation("Quiz.InvalidCursor", "Cursor is invalid.")));

        IActionResult result = await controller.ListQuizzes(cursor: "malformed", pageSize: 20, cancellationToken: CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetQuizById_Success_DispatchesQueryAndReturnsOk()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        QuizDetailsResponse expectedResponse = new(
            quizId,
            "Science Quiz",
            "Test description",
            1,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            new List<QuizQuestionDetailsResponse>());

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.GetQuizById(quizId, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        GetQuizByIdQuery query = Assert.IsType<GetQuizByIdQuery>(_sender.Requests[0]);
        Assert.Equal(quizId, query.QuizId);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task GetQuizById_NotFound_ReturnsProblemDetails()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}");
        QuizzesController controller = CreateController(context);

        _sender.RespondWith(Result.Failure<QuizDetailsResponse>(Error.NotFound("Quiz.NotFound", "Quiz was not found.")));

        IActionResult result = await controller.GetQuizById(quizId, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateQuiz_Success_DispatchesCommandAndReturnsOk()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        UpdateQuizRequest request = new("Updated Title", "Updated Description");
        QuizSummaryResponse expectedResponse = new(quizId, request.Title, request.Description, 2, 5, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.UpdateQuiz(quizId, request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        UpdateQuizCommand command = Assert.IsType<UpdateQuizCommand>(_sender.Requests[0]);
        Assert.Equal(quizId, command.QuizId);
        Assert.Equal(request.Title, command.Title);
        Assert.Equal(request.Description, command.Description);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task UpdateQuiz_Conflict_ReturnsProblemDetails()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}");
        QuizzesController controller = CreateController(context);

        UpdateQuizRequest request = new("Updated Title", null);
        _sender.RespondWith(Result.Failure<QuizSummaryResponse>(Error.Conflict("Quiz.ConcurrencyConflict", "Revision mismatch.")));

        IActionResult result = await controller.UpdateQuiz(quizId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteQuiz_Success_DispatchesCommandAndReturnsNoContent()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        _sender.RespondWith(Result.Success());

        IActionResult result = await controller.DeleteQuiz(quizId, cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        DeleteQuizCommand command = Assert.IsType<DeleteQuizCommand>(_sender.Requests[0]);
        Assert.Equal(quizId, command.QuizId);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task DeleteQuiz_Failure_ReturnsProblemDetails()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}");
        QuizzesController controller = CreateController(context);

        _sender.RespondWith(Result.Failure(Error.Conflict("Quiz.HasActiveGames", "Cannot delete quiz with active games.")));

        IActionResult result = await controller.DeleteQuiz(quizId, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task AddQuestion_Success_DispatchesCommandAndReturnsCreated()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}/questions");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        List<ChoiceRequest> choices = new()
        {
            new ChoiceRequest("Option A", true),
            new ChoiceRequest("Option B", false),
            new ChoiceRequest("Option C", false)
        };
        Guid imageId = Guid.NewGuid();
        AddQuestionRequest request = new("What is 2 + 2?", imageId, 20, 1000, choices);

        Guid questionId = Guid.NewGuid();
        QuestionResponse expectedResponse = new(
            questionId,
            quizId,
            0,
            request.Text,
            imageId,
            "https://cdn.example.test/img.png",
            20,
            1000,
            new List<ChoiceResponse>
            {
                new ChoiceResponse(Guid.NewGuid(), 0, "Option A", true),
                new ChoiceResponse(Guid.NewGuid(), 1, "Option B", false),
                new ChoiceResponse(Guid.NewGuid(), 2, "Option C", false)
            });

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.AddQuestion(quizId, request, cts.Token);

        CreatedResult created = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal($"/api/quizzes/{quizId}/questions/{questionId}", created.Location);
        Assert.Same(expectedResponse, created.Value);

        Assert.Single(_sender.Requests);
        AddQuestionCommand command = Assert.IsType<AddQuestionCommand>(_sender.Requests[0]);
        Assert.Equal(quizId, command.QuizId);
        Assert.Equal(request.Text, command.Text);
        Assert.Equal(imageId, command.ImageId);
        Assert.Equal(20, command.DurationSeconds);
        Assert.Equal(1000, command.BasePoints);
        Assert.Equal(3, command.Choices.Count);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task AddQuestion_Failure_ReturnsProblemDetails()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}/questions");
        QuizzesController controller = CreateController(context);

        AddQuestionRequest request = new("Text", null, 30, 500, new List<ChoiceRequest>());
        _sender.RespondWith(Result.Failure<QuestionResponse>(Error.Validation("Question.ChoicesRequired", "Choices required.")));

        IActionResult result = await controller.AddQuestion(quizId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateQuestion_Success_DispatchesCommandAndReturnsOk()
    {
        Guid quizId = Guid.NewGuid();
        Guid questionId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}/questions/{questionId}");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        List<ChoiceRequest> choices = new()
        {
            new ChoiceRequest("Updated Option 1", true),
            new ChoiceRequest("Updated Option 2", false)
        };
        UpdateQuestionRequest request = new("Updated question text", null, 15, 2000, choices);

        QuestionResponse expectedResponse = new(
            questionId,
            quizId,
            0,
            request.Text,
            null,
            null,
            15,
            2000,
            new List<ChoiceResponse>());

        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.UpdateQuestion(quizId, questionId, request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        UpdateQuestionCommand command = Assert.IsType<UpdateQuestionCommand>(_sender.Requests[0]);
        Assert.Equal(quizId, command.QuizId);
        Assert.Equal(questionId, command.QuestionId);
        Assert.Equal(request.Text, command.Text);
        Assert.Null(command.ImageId);
        Assert.Equal(15, command.DurationSeconds);
        Assert.Equal(2000, command.BasePoints);
        Assert.Equal(2, command.Choices.Count);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task UpdateQuestion_Failure_ReturnsProblemDetails()
    {
        Guid quizId = Guid.NewGuid();
        Guid questionId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}/questions/{questionId}");
        QuizzesController controller = CreateController(context);

        UpdateQuestionRequest request = new("Updated", null, 15, 1000, new List<ChoiceRequest>());
        _sender.RespondWith(Result.Failure<QuestionResponse>(Error.NotFound("Question.NotFound", "Question not found.")));

        IActionResult result = await controller.UpdateQuestion(quizId, questionId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task DeleteQuestion_Success_DispatchesCommandAndReturnsNoContent()
    {
        Guid quizId = Guid.NewGuid();
        Guid questionId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}/questions/{questionId}");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        _sender.RespondWith(Result.Success());

        IActionResult result = await controller.DeleteQuestion(quizId, questionId, cts.Token);

        NoContentResult noContent = Assert.IsType<NoContentResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, noContent.StatusCode);

        Assert.Single(_sender.Requests);
        DeleteQuestionCommand command = Assert.IsType<DeleteQuestionCommand>(_sender.Requests[0]);
        Assert.Equal(quizId, command.QuizId);
        Assert.Equal(questionId, command.QuestionId);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task DeleteQuestion_Failure_ReturnsProblemDetails()
    {
        Guid quizId = Guid.NewGuid();
        Guid questionId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}/questions/{questionId}");
        QuizzesController controller = CreateController(context);

        _sender.RespondWith(Result.Failure(Error.NotFound("Question.NotFound", "Question not found.")));

        IActionResult result = await controller.DeleteQuestion(quizId, questionId, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReorderQuestions_Success_DispatchesCommandPreservingSequenceAndReturnsOk()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}/reorder");
        QuizzesController controller = CreateController(context);
        CancellationTokenSource cts = new();

        List<Guid> questionIds = new() { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ReorderQuestionsRequest request = new(questionIds);
        ReorderQuestionsResponse expectedResponse = new("Questions reordered successfully.", 3);
        _sender.RespondWith(Result.Success(expectedResponse));

        IActionResult result = await controller.ReorderQuestions(quizId, request, cts.Token);

        OkObjectResult okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        Assert.Same(expectedResponse, okResult.Value);

        Assert.Single(_sender.Requests);
        ReorderQuestionsCommand command = Assert.IsType<ReorderQuestionsCommand>(_sender.Requests[0]);
        Assert.Equal(quizId, command.QuizId);
        Assert.Equal(questionIds, command.QuestionIds);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task ReorderQuestions_Failure_ReturnsProblemDetails()
    {
        Guid quizId = Guid.NewGuid();
        DefaultHttpContext context = HttpContextFactory.Create(path: $"/api/quizzes/{quizId}/reorder");
        QuizzesController controller = CreateController(context);

        ReorderQuestionsRequest request = new(new List<Guid> { Guid.NewGuid() });
        _sender.RespondWith(Result.Failure<ReorderQuestionsResponse>(Error.Conflict("Quiz.QuestionListMismatch", "Question set changed.")));

        IActionResult result = await controller.ReorderQuestions(quizId, request, CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }
}
