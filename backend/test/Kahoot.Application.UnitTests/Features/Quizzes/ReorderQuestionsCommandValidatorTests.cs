using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.ReorderQuestions;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes;

public sealed class ReorderQuestionsCommandValidatorTests
{
    private readonly ReorderQuestionsCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        ReorderQuestionsCommand command = new(
            Guid.NewGuid(),
            new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() });

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage()
    {
        ReorderQuestionsCommand command = new(
            Guid.Empty,
            new[] { Guid.NewGuid() });

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuizId is required.");
    }

    [Fact]
    public void Validate_WhenQuestionIdsIsNull_FailsWithRequiredMessage()
    {
        ReorderQuestionsCommand command = new(
            Guid.NewGuid(),
            null!);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuestionIds are required.");
    }

    [Fact]
    public void Validate_WhenQuestionIdsIsEmpty_PassesValidatorCheck()
    {
        // ReorderQuestionsCommandValidator checks NotNull, so an empty collection passes validator layer
        ReorderQuestionsCommand command = new(
            Guid.NewGuid(),
            Array.Empty<Guid>());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
