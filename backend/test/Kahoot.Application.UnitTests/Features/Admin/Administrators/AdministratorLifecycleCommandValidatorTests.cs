using FluentValidation.Results;
using Kahoot.Application.Features.Admin.Administrators.ReactivateAdministrator;
using Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Admin.Administrators;

public sealed class AdministratorLifecycleCommandValidatorTests
{
    [Fact]
    public void SuspendAdministrator_WithValidCommand_Succeeds()
    {
        SuspendAdministratorCommandValidator validator = new();
        SuspendAdministratorCommand command = new(Guid.NewGuid(), 1L);

        ValidationResult result = validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SuspendAdministrator_WhenAdministratorIdIsEmpty_FailsWithRequiredMessage()
    {
        SuspendAdministratorCommandValidator validator = new();
        SuspendAdministratorCommand command = new(Guid.Empty, 1L);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Target administrator identifier is required.");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void SuspendAdministrator_WhenRevisionNotGreaterThanZero_Fails(long invalidRevision)
    {
        SuspendAdministratorCommandValidator validator = new();
        SuspendAdministratorCommand command = new(Guid.NewGuid(), invalidRevision);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Revision must be greater than 0.");
    }

    [Fact]
    public void ReactivateAdministrator_WithValidCommand_Succeeds()
    {
        ReactivateAdministratorCommandValidator validator = new();
        ReactivateAdministratorCommand command = new(Guid.NewGuid(), 1L);

        ValidationResult result = validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ReactivateAdministrator_WhenAdministratorIdIsEmpty_FailsWithRequiredMessage()
    {
        ReactivateAdministratorCommandValidator validator = new();
        ReactivateAdministratorCommand command = new(Guid.Empty, 1L);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Target administrator identifier is required.");
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void ReactivateAdministrator_WhenRevisionNotGreaterThanZero_Fails(long invalidRevision)
    {
        ReactivateAdministratorCommandValidator validator = new();
        ReactivateAdministratorCommand command = new(Guid.NewGuid(), invalidRevision);

        ValidationResult result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Revision must be greater than 0.");
    }
}
