using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Application.Features.Quizzes.Questions.AddQuestion;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes.Questions;

public sealed class ChoiceRequestValidatorTests
{
    private readonly ChoiceRequestValidator _validator = new();

    [Fact]
    public void Validate_WithValidChoice_Succeeds()
    {
        ChoiceRequest choice = new("Paris", true);

        ValidationResult result = _validator.Validate(choice);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenTextIsEmptyOrWhitespace_FailsWithRequiredMessage(string? text)
    {
        ChoiceRequest choice = new(text!, false);

        ValidationResult result = _validator.Validate(choice);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Choice text is required.");
    }

    [Fact]
    public void Validate_WhenTextExceeds300Characters_FailsWithLengthMessage()
    {
        string longText = new('c', 301);
        ChoiceRequest choice = new(longText, false);

        ValidationResult result = _validator.Validate(choice);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Choice text must be between 1 and 300 characters."));
    }

    [Fact]
    public void Validate_WhenTextAt300CharactersBoundary_Succeeds()
    {
        string maxText = new('c', 300);
        ChoiceRequest choice = new(maxText, false);

        ValidationResult result = _validator.Validate(choice);

        Assert.True(result.IsValid);
    }
}
