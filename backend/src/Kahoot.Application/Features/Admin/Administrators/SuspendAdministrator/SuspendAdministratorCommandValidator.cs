using FluentValidation;

namespace Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;

public sealed class SuspendAdministratorCommandValidator : AbstractValidator<SuspendAdministratorCommand>
{
    public SuspendAdministratorCommandValidator()
    {
        RuleFor(command => command.AdministratorId)
            .NotEmpty()
            .WithMessage("Target administrator identifier is required.");

        RuleFor(command => command.Revision)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Revision must be greater than or equal to 0.");
    }
}
