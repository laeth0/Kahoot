using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Application.Features.Quizzes.Questions.AddQuestion;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes.Questions;

public sealed class AddQuestionCommandValidatorTests
{
    private readonly AddQuestionCommandValidator _validator = new();

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
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "What is the capital of France?",
            null,
            30,
            1000,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage()
    {
        AddQuestionCommand command = new(
            Guid.Empty,
            "Valid question text?",
            null,
            30,
            1000,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuizId is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenTextIsEmptyOrWhitespace_FailsWithRequiredMessage(string? text)
    {
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            text!,
            null,
            30,
            1000,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Question text is required.");
    }

    [Fact]
    public void Validate_WhenTextExceeds500Characters_FailsWithLengthMessage()
    {
        string longText = new('q', 501);
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            longText,
            null,
            30,
            1000,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Question text must be between 1 and 500 characters."));
    }

    [Fact]
    public void Validate_WhenTextAt500CharactersBoundary_Succeeds()
    {
        string maxText = new('q', 500);
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            maxText,
            null,
            30,
            1000,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(300)]
    public void Validate_WhenDurationIsWithinBounds_Succeeds(int durationSeconds)
    {
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            durationSeconds,
            1000,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(301)]
    public void Validate_WhenDurationIsOutOfBounds_Fails(int durationSeconds)
    {
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            durationSeconds,
            1000,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("DurationSeconds must be between 5 and 300 seconds."));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500)]
    [InlineData(1000)]
    [InlineData(int.MaxValue)]
    public void Validate_WhenBasePointsIsNonNegative_Succeeds(int basePoints)
    {
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            basePoints,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenBasePointsIsNegative_Fails()
    {
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            -1,
            CreateValidChoices());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("BasePoints must be between 0 and"));
    }

    [Fact]
    public void Validate_WhenChoicesIsNull_FailsWithRequiredMessage()
    {
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            1000,
            null!);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Choices are required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(10)]
    public void Validate_WhenChoicesCountIsOutsideTwoToSix_Fails(int count)
    {
        List<ChoiceRequest> choices = new();
        for (int index = 0; index < count; index++)
        {
            choices.Add(new ChoiceRequest($"Choice {index + 1}", index == 0));
        }

        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            1000,
            choices);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("A question must contain between 2 and 6 choices."));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void Validate_WhenChoicesCountIsBetweenTwoAndSix_Succeeds(int count)
    {
        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            1000,
            CreateValidChoices(count, 1));

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenNoChoicesAreCorrect_Fails()
    {
        List<ChoiceRequest> noCorrectChoices = new()
        {
            new ChoiceRequest("Choice 1", false),
            new ChoiceRequest("Choice 2", false),
            new ChoiceRequest("Choice 3", false),
            new ChoiceRequest("Choice 4", false)
        };

        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            1000,
            noCorrectChoices);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "At least one choice must be marked as correct.");
    }

    [Fact]
    public void Validate_WhenMultipleChoicesAreCorrect_Succeeds()
    {
        List<ChoiceRequest> multiCorrectChoices = new()
        {
            new ChoiceRequest("Choice 1", true),
            new ChoiceRequest("Choice 2", true),
            new ChoiceRequest("Choice 3", false),
            new ChoiceRequest("Choice 4", false)
        };

        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            1000,
            multiCorrectChoices);

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenNestedChoiceHasEmptyText_FailsNestedValidation()
    {
        List<ChoiceRequest> choicesWithEmptyChoice = new()
        {
            new ChoiceRequest("Valid Choice 1", true),
            new ChoiceRequest("", false)
        };

        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            1000,
            choicesWithEmptyChoice);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Choice text is required.");
    }

    [Fact]
    public void Validate_WhenNestedChoiceExceeds300Characters_FailsNestedValidation()
    {
        string longChoice = new('c', 301);
        List<ChoiceRequest> choices = new()
        {
            new ChoiceRequest("Valid Choice 1", true),
            new ChoiceRequest(longChoice, false)
        };

        AddQuestionCommand command = new(
            Guid.NewGuid(),
            "Valid question text?",
            null,
            30,
            1000,
            choices);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Choice text must be between 1 and 300 characters."));
    }
}
