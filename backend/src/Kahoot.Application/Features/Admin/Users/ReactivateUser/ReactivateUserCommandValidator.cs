using FluentValidation;

namespace Kahoot.Application.Features.Admin.Users.ReactivateUser;

public sealed class ReactivateUserCommandValidator : AbstractValidator<ReactivateUserCommand>
{
    public ReactivateUserCommandValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("Target account identifier is required.");

        RuleFor(x => x.Revision)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Revision must be greater than or equal to 0.");
    }
}
