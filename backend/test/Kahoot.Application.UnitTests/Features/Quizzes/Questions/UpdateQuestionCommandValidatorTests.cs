using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes.Questions;

public sealed class UpdateQuestionCommandValidatorTests
{
    private readonly UpdateQuestionCommandValidator _validator = new();

    private static IReadOnlyList<ChoiceRequest> CreateValidChoices(int count = 4, int correctCount = 1)
    {
        List<ChoiceRequest> list = new();
        for (int index = 0; index < count; index++)
        {
            list.Add(new ChoiceRequest($"Choice {index + 1}", index < correctCount));
        }

        return list;
    }

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        UpdateQuestionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "What is the updated question?",
            null,
            20,
            500,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage()
    {
        UpdateQuestionCommand command = new(
            Guid.Empty,
            Guid.NewGuid(),
            "Valid question text?",
            null,
            20,
            500,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuizId is required.");
    }

    [Fact]
    public void Validate_WhenQuestionIdIsEmpty_FailsWithRequiredMessage()
    {
        UpdateQuestionCommand command = new(
            Guid.NewGuid(),
            Guid.Empty,
            "Valid question text?",
            null,
            20,
            500,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuestionId is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenTextIsEmptyOrWhitespace_FailsWithRequiredMessage(string? text)
    {
        UpdateQuestionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            text!,
            null,
            20,
            500,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Question text is required.");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(301)]
    public void Validate_WhenDurationIsOutOfBounds_Fails(int durationSeconds)
    {
        UpdateQuestionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid question text?",
            null,
            durationSeconds,
            500,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("DurationSeconds must be between 5 and 300 seconds."));
    }

    [Fact]
    public void Validate_WhenBasePointsIsNegative_Fails()
    {
        UpdateQuestionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid question text?",
            null,
            20,
            -5,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("BasePoints must be between 0 and"));
    }

    [Fact]
    public void Validate_WhenChoicesCountLessThanTwo_Fails()
    {
        UpdateQuestionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid question text?",
            null,
            20,
            500,
            new[] { new ChoiceRequest("Single Choice", true) });

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("A question must contain between 2 and 6 choices."));
    }

    [Fact]
    public void Validate_WhenNoChoicesAreCorrect_Fails()
    {
        UpdateQuestionCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Valid question text?",
            null,
            20,
            500,
            new[]
            {
                new ChoiceRequest("Choice 1", false),
                new ChoiceRequest("Choice 2", false)
            });

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "At least one choice must be marked as correct.");
    }
}
