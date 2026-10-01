using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.Questions.DeleteQuestion;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes.Questions;

public sealed class DeleteQuestionCommandValidatorTests
{
    private readonly DeleteQuestionCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidIdentifiers_Succeeds()
    {
        DeleteQuestionCommand command = new(Guid.NewGuid(), Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage()
    {
        DeleteQuestionCommand command = new(Guid.Empty, Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuizId is required.");
    }

    [Fact]
    public void Validate_WhenQuestionIdIsEmpty_FailsWithRequiredMessage()
    {
        DeleteQuestionCommand command = new(Guid.NewGuid(), Guid.Empty);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuestionId is required.");
    }
}
