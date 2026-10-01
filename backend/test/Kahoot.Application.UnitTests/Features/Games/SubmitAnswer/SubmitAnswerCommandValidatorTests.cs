using FluentValidation.Results;
using Kahoot.Application.Features.Games.SubmitAnswer;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Games.SubmitAnswer;

public sealed class SubmitAnswerCommandValidatorTests
{
    private readonly SubmitAnswerCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommandAndSessionTokenHash_Succeeds()
    {
        SubmitAnswerCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid() },
            "connection-123",
            new byte[] { 1, 2, 3 },
            null);

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithValidCommandAndConnectionGeneration_Succeeds()
    {
        SubmitAnswerCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid() },
            "connection-123",
            null,
            5L);

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenGameIdIsEmpty_FailsWithRequiredMessage()
    {
        SubmitAnswerCommand command = new(
            Guid.Empty,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid() },
            "connection-123",
            new byte[] { 1, 2, 3 },
            null);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Game ID is required.");
    }

    [Fact]
    public void Validate_WhenParticipantIdIsEmpty_FailsWithRequiredMessage()
    {
        SubmitAnswerCommand command = new(
            Guid.NewGuid(),
            Guid.Empty,
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid() },
            "connection-123",
            new byte[] { 1, 2, 3 },
            null);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Participant ID is required.");
    }

    [Fact]
    public void Validate_WhenBothSessionTokenHashAndConnectionGenerationAreNull_FailsWithSessionRequiredMessage()
    {
        SubmitAnswerCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<Guid> { Guid.NewGuid() },
            "connection-123",
            null,
            null);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "A participant session is required.");
    }

    [Fact]
    public void Validate_WhenChoiceIdsEmpty_PassesValidatorCheck()
    {
        // Choice-payload checks live in the handler; the validator does not check choices list
        SubmitAnswerCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<Guid>(),
            "connection-123",
            new byte[] { 1, 2, 3 },
            null);

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
