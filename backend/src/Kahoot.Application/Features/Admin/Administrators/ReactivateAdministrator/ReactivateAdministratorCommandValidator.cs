using FluentValidation;

namespace Kahoot.Application.Features.Admin.Administrators.ReactivateAdministrator;

public sealed class ReactivateAdministratorCommandValidator : AbstractValidator<ReactivateAdministratorCommand>
{
    public ReactivateAdministratorCommandValidator()
    {
        RuleFor(command => command.AdministratorId)
            .NotEmpty()
            .WithMessage("Target administrator identifier is required.");

        RuleFor(command => command.Revision)
            .GreaterThan(0)
            .WithMessage("Revision must be greater than 0.");
    }
}
