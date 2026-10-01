using FluentValidation.Results;
using Kahoot.Application.Features.Admin.Users.ReactivateUser;
using Kahoot.Application.Features.Admin.Users.SuspendUser;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Admin.Users;

public sealed class UserLifecycleCommandValidatorTests
{
    [Fact]
    public void SuspendUser_WithValidCommand_Succeeds()
    {
        SuspendUserCommandValidator validator = new();
        SuspendUserCommand command = new(Guid.NewGuid(), 1L);

        ValidationResult result = validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SuspendUser_WhenAccountIdIsEmpty_FailsWithRequiredMessage()
    {
        SuspendUserCommandValidator validator = new();
        SuspendUserCommand command = new(Guid.Empty, 1L);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "AccountId must not be empty.");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void SuspendUser_WhenRevisionNotGreaterThanZero_Fails(long invalidRevision)
    {
        SuspendUserCommandValidator validator = new();
        SuspendUserCommand command = new(Guid.NewGuid(), invalidRevision);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Revision must be greater than zero.");
    }

    [Fact]
    public void ReactivateUser_WithValidCommand_Succeeds()
    {
        ReactivateUserCommandValidator validator = new();
        ReactivateUserCommand command = new(Guid.NewGuid(), 1L);

        ValidationResult result = validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ReactivateUser_WhenAccountIdIsEmpty_FailsWithRequiredMessage()
    {
        ReactivateUserCommandValidator validator = new();
        ReactivateUserCommand command = new(Guid.Empty, 1L);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Target account identifier is required.");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    public void ReactivateUser_WhenRevisionNonNegative_Succeeds(long validRevision)
    {
        ReactivateUserCommandValidator validator = new();
        ReactivateUserCommand command = new(Guid.NewGuid(), validRevision);

        ValidationResult result = validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ReactivateUser_WhenRevisionNegative_Fails()
    {
        ReactivateUserCommandValidator validator = new();
        ReactivateUserCommand command = new(Guid.NewGuid(), -1L);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Revision must be greater than or equal to 0.");
    }
}
