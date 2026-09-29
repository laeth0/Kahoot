namespace Kahoot.Application.Features.Games.AdvanceQuestion;

using FluentValidation;

public sealed class AdvanceQuestionCommandValidator : AbstractValidator<AdvanceQuestionCommand>
{
    public AdvanceQuestionCommandValidator()
    {
        RuleFor(command => command.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        RuleFor(command => command.CommandId)
            .NotEmpty()
            .WithMessage("CommandId is required.");

        RuleFor(command => command.ExpectedStateVersion)
            .GreaterThanOrEqualTo(1)
            .WithMessage("ExpectedStateVersion must be greater than or equal to 1.");
    }
}
