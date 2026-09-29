using FluentValidation;

namespace Kahoot.Application.Features.Admin.Users.SuspendUser;

public sealed class SuspendUserCommandValidator : AbstractValidator<SuspendUserCommand>
{
    public SuspendUserCommandValidator()
    {
        // Target Identity Validation - Ensures target host account identifier is non-empty
        RuleFor(command => command.AccountId)
            .NotEmpty()
            .WithMessage("AccountId must not be empty.");

        // Optimistic Concurrency Invariant - Requires positive monotonic revision fence (ACCT-BOUND-001)
        RuleFor(command => command.Revision)
            .GreaterThan(0)
            .WithMessage("Revision must be greater than zero.");
    }
}
