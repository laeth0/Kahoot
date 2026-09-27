using FluentValidation;

namespace Kahoot.Application.Features.Admin.Users.SuspendUser;

public sealed class SuspendUserCommandValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserCommandValidator()
    {
        RuleFor(command => command.AccountId)
            .NotEmpty()
            .WithMessage("AccountId must not be empty.");

        RuleFor(command => command.Revision)
            .GreaterThan(0)
            .WithMessage("Revision must be greater than zero.");
    }
}
