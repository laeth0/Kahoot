using FluentValidation.Results;
using Kahoot.Application.Features.Quizzes.GetQuizById;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes;

public sealed class GetQuizByIdQueryValidatorTests
{
    private readonly GetQuizByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuizId_Succeeds()
    {
        GetQuizByIdQuery query = new(Guid.NewGuid());

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenQuizIdIsEmpty_FailsWithRequiredMessage()
    {
        GetQuizByIdQuery query = new(Guid.Empty);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "QuizId is required.");
    }
}
