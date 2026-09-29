using FluentValidation;

namespace Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;

public sealed class SuspendAdministratorCommandValidator : AbstractValidator<SuspendAdministratorCommand>
{
    public SuspendAdministratorCommandValidator()
    {
        // Target Identity Validation - Ensures target administrator identifier is non-empty
        RuleFor(command => command.AdministratorId)
            .NotEmpty()
            .WithMessage("Target administrator identifier is required.");

        // Optimistic Concurrency Invariant - Requires positive monotonic revision fence (ACCT-BOUND-001)
        RuleFor(command => command.Revision)
            .GreaterThan(0)
            .WithMessage("Revision must be greater than 0.");
    }
}
