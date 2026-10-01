using FluentValidation.Results;
using Kahoot.Application.Features.Admin.Users.GetUserById;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Admin.Users;

public sealed class GetUserByIdQueryValidatorTests
{
    private readonly GetUserByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidAccountId_Succeeds()
    {
        GetUserByIdQuery query = new(Guid.NewGuid());

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WhenAccountIdIsEmpty_FailsWithRequiredMessage()
    {
        GetUserByIdQuery query = new(Guid.Empty);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "AccountId must not be empty.");
    }
}
