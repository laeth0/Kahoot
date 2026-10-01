using FluentValidation.Results;
using Kahoot.Application.Common.Pagination;
using Kahoot.Application.Features.Admin.Users.ListUsers;
using Kahoot.Domain.Enums;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Admin.Users;

public sealed class ListUsersQueryValidatorTests
{
    private readonly ListUsersQueryValidator _validator = new();

    [Fact]
    public void Validate_WithDefaultQuery_Succeeds()
    {
        ListUsersQuery query = new();

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
        ListUsersQuery query = new(PageSize: pageSize);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_WhenPageSizeIsOutOfBounds_Fails(int pageSize)
    {
        ListUsersQuery query = new(PageSize: pageSize);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "PageSize must be between 1 and 100.");
    }

    [Fact]
    public void Validate_WhenUsernameFilterWithin64Characters_Succeeds()
    {
        string usernameFilter = new('u', 64);
        ListUsersQuery query = new(Username: usernameFilter);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenUsernameFilterExceeds64Characters_Fails()
    {
        string usernameFilter = new('u', 65);
        ListUsersQuery query = new(Username: usernameFilter);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Username search filter cannot exceed 64 characters.");
    }

    [Theory]
    [InlineData(UserStatus.Active)]
    [InlineData(UserStatus.Suspended)]
    public void Validate_WhenStatusIsValidEnum_Succeeds(UserStatus status)
    {
        ListUsersQuery query = new(Status: status);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenStatusIsInvalidEnum_Fails()
    {
        ListUsersQuery query = new(Status: (UserStatus)999);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WhenCursorIsValidKeyset_Succeeds()
    {
        string cursor = KeysetCursor.Encode(DateTimeOffset.UtcNow, Guid.NewGuid());
        ListUsersQuery query = new(Cursor: cursor);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenCursorIsMalformed_Fails()
    {
        ListUsersQuery query = new(Cursor: "malformed-cursor-token");

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Cursor is invalid or malformed.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenCursorIsNullOrEmptyOrWhitespace_Succeeds(string? cursor)
    {
        ListUsersQuery query = new(Cursor: cursor);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
    }
}
