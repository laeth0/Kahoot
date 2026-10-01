using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.UpdateQuiz;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes;

public sealed class UpdateQuizCommandValidatorTests
{
    private readonly UpdateQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        UpdateQuizCommand command = new(Guid.NewGuid(), "Updated Title", "Updated Description");

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage()
    {
        UpdateQuizCommand command = new(Guid.Empty, "Updated Title", "Updated Description");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuizId is required.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenTitleIsEmptyOrWhitespace_FailsWithRequiredMessage(string? title)
    {
        UpdateQuizCommand command = new(Guid.NewGuid(), title!, "Description");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Title is required.");
    }

    [Fact]
    public void Validate_WhenTitleExceeds200Characters_FailsWithLengthMessage()
    {
        string longTitle = new('a', 201);
        UpdateQuizCommand command = new(Guid.NewGuid(), longTitle, "Description");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Title must be between 1 and 200 characters."));
    }

    [Fact]
    public void Validate_WhenDescriptionExceeds1000Characters_FailsWithLengthMessage()
    {
        string longDescription = new('a', 1001);
        UpdateQuizCommand command = new(Guid.NewGuid(), "Valid Title", longDescription);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Description cannot exceed 1000 characters."));
    }
}
