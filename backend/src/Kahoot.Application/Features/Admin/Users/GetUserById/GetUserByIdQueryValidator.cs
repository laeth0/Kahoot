using FluentValidation;

namespace Kahoot.Application.Features.Admin.Users.GetUserById;

public sealed class GetUserByIdQueryValidator : AbstractValidator<GetUserByIdQuery>
{
    public GetUserByIdQueryValidator()
    {
        RuleFor(query => query.AccountId)
            .NotEmpty()
            .WithMessage("AccountId must not be empty.");
    }
}
