namespace Kahoot.Application.Features.Games.EndQuestion;

using FluentValidation;

public sealed class EndQuestionCommandValidator : AbstractValidator<EndQuestionCommand>
{
    public EndQuestionCommandValidator()
    {
        // Resource Boundary Validation - Validates target game UUID is non-empty before transactional lookup
        RuleFor(command => command.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        // Command Idempotency Boundary - Requires unique client command UUID to enforce exactly-once execution semantics
        RuleFor(command => command.CommandId)
            .NotEmpty()
            .WithMessage("CommandId is required.");

        // Optimistic Concurrency Control (OCC) Guard - Enforces positive monotonic version fence against race conditions and stale requests
        RuleFor(command => command.ExpectedStateVersion)
            .GreaterThanOrEqualTo(1)
            .WithMessage("ExpectedStateVersion must be greater than or equal to 1.");
    }
}
