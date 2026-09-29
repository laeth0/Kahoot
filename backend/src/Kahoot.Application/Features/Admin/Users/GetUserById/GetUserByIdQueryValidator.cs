using FluentValidation;

namespace Kahoot.Application.Features.Admin.Users.GetUserById;

public sealed class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
{
    public GetUserByIdQueryValidator()
    {
        // Target Identity Validation - Ensures target account identifier is non-empty before executing lookup
        RuleFor(query => query.AccountId)
            .NotEmpty()
            .WithMessage("AccountId must not be empty.");
    }
}
