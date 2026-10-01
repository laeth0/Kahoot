using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.DeleteQuiz;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes;

public sealed class DeleteQuizCommandValidatorTests
{
    private readonly DeleteQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuizId_Succeeds()
    {
        DeleteQuizCommand command = new(Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage()
    {
        DeleteQuizCommand command = new(Guid.Empty);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuizId is required.");
    }
}
