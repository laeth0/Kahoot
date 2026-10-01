using FluentValidation.Results;
using Kahoot.Application.Common.Pagination;
using Kahoot.Application.Features.Quizzes.ListQuizzes;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Quizzes;

public sealed class ListQuizzesQueryValidatorTests
{
    private readonly ListQuizzesQueryValidator _validator = new();

    [Fact]
    public void Validate_WithDefaultQuery_Succeeds()
    {
        ListQuizzesQuery query = new();

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithValidCursorAndPageSize_Succeeds()
    {
        string cursor = KeysetCursor.Encode(DateTimeOffset.UtcNow, Guid.NewGuid());
        ListQuizzesQuery query = new(cursor, 25);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void Validate_WhenPageSizeIsWithinBounds_Succeeds(int pageSize)
    {
        ListQuizzesQuery query = new(null, pageSize);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(500)]
    public void Validate_WhenPageSizeIsOutOfBounds_Fails(int pageSize)
    {
        ListQuizzesQuery query = new(null, pageSize);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("PageSize must be between 1 and 100."));
    }

    [Fact]
    public void Validate_WhenCursorIsMalformed_FailsWithMalformedMessage()
    {
        ListQuizzesQuery query = new("not-a-valid-keyset-cursor", 20);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Cursor is invalid or malformed.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WhenCursorIsEmptyOrWhitespace_Succeeds(string? cursor)
    {
        ListQuizzesQuery query = new(cursor, 20);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
