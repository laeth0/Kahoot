namespace Kahoot.Application.Features.Games.RemoveParticipant;

using FluentValidation;

public sealed class RemoveParticipantCommandValidator : AbstractValidator<RemoveParticipantCommand>
{
    public RemoveParticipantCommandValidator()
    {
        // Resource Boundary Validation - Validates target game UUID is non-empty before eviction transaction
        RuleFor(command => command.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        // Subject Boundary Validation - Validates target participant UUID is non-empty before looking up seat
        RuleFor(command => command.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required.");
    }
}
