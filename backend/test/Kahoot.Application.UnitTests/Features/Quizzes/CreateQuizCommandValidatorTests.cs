using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.CreateQuiz;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes;

public sealed class CreateQuizCommandValidatorTests
{
    private readonly CreateQuizCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        CreateQuizCommand command = new("Math 101 Quiz", "An introductory mathematics quiz.");

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithNullDescription_Succeeds()
    {
        CreateQuizCommand command = new("Math 101 Quiz", null);

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t\r\n   ")]
    [InlineData(null)]
    public void Validate_WhenTitleIsEmptyOrWhitespace_FailsWithRequiredMessage(string? title)
    {
        CreateQuizCommand command = new(title!, "Description");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Title is required.");
    }

    [Fact]
    public void Validate_WhenTitleExceeds200Characters_FailsWithLengthMessage()
    {
        string longTitle = new('a', 201);
        CreateQuizCommand command = new(longTitle, "Description");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Title must be between 1 and 200 characters."));
    }

    [Fact]
    public void Validate_WhenTitleAtBoundary_Succeeds()
    {
        string maxTitle = new('a', 200);
        CreateQuizCommand command = new(maxTitle, null);

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenDescriptionExceeds1000Characters_FailsWithLengthMessage()
    {
        string longDescription = new('a', 1001);
        CreateQuizCommand command = new("Valid Title", longDescription);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Description cannot exceed 1000 characters."));
    }

    [Fact]
    public void Validate_WhenDescriptionAtBoundary_Succeeds()
    {
        string maxDescription = new('a', 1000);
        CreateQuizCommand command = new("Valid Title", maxDescription);

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
