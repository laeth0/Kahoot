using FluentValidation;

namespace Kahoot.Application.Features.Admin.Users.ReactivateUser;

public sealed class ReactivateUserCommandValidator : AbstractValidator<ReactivateUserCommand>
{
    public ReactivateUserCommandValidator()
    {
        // Target Identity Validation - Ensures target host account identifier is non-empty
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithMessage("Target account identifier is required.");

        // Optimistic Concurrency Invariant - Requires non-negative revision fence (ACCT-BOUND-001)
        RuleFor(x => x.Revision)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Revision must be greater than or equal to 0.");
    }
}
