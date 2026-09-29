namespace Kahoot.Application.Features.Games.RemoveParticipant;

using FluentValidation;

public sealed class RemoveParticipantCommandValidator : AbstractValidator<RemoveParticipantCommand>
{
    public RemoveParticipantCommandValidator()
    {
        RuleFor(command => command.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        RuleFor(command => command.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required.");
    }
}
