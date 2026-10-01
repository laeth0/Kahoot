using FluentValidation.Results;
using Kahoot.Application.Features.Games.GetGameParticipants;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Games.GetGameParticipants;

public sealed class GetGameParticipantsQueryValidatorTests
{
    private readonly GetGameParticipantsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidQuery_Succeeds()
    {
        GetGameParticipantsQuery query = new(
            GameId: Guid.NewGuid(),
            IncludeRemoved: false,
            Limit: 50,
            Cursor: 10);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithNullCursorAndLimit_Succeeds()
    {
        GetGameParticipantsQuery query = new(
            GameId: Guid.NewGuid(),
            IncludeRemoved: false,
            Limit: null,
            Cursor: null);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenGameIdIsEmpty_FailsWithRequiredMessage()
    {
        GetGameParticipantsQuery query = new(
            GameId: Guid.Empty,
            IncludeRemoved: false,
            Limit: null,
            Cursor: null);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "GameId is required.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(500)]
    public void Validate_WhenLimitWithinBounds_Succeeds(int limit)
    {
        GetGameParticipantsQuery query = new(
            GameId: Guid.NewGuid(),
            IncludeRemoved: false,
            Limit: limit,
            Cursor: null);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public void Validate_WhenLimitOutOfBounds_Fails(int limit)
    {
        GetGameParticipantsQuery query = new(
            GameId: Guid.NewGuid(),
            IncludeRemoved: false,
            Limit: limit,
            Cursor: null);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Limit must be between 1 and 500.");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Validate_WhenCursorIsPositive_Succeeds(int cursor)
    {
        GetGameParticipantsQuery query = new(
            GameId: Guid.NewGuid(),
            IncludeRemoved: false,
            Limit: null,
            Cursor: cursor);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WhenCursorNotPositive_Fails(int cursor)
    {
        GetGameParticipantsQuery query = new(
            GameId: Guid.NewGuid(),
            IncludeRemoved: false,
            Limit: null,
            Cursor: cursor);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Cursor must be a positive seat number.");
    }
}
