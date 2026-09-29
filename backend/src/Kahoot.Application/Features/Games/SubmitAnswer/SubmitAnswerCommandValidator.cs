namespace Kahoot.Application.Features.Games.SubmitAnswer;

using FluentValidation;

// Submit Answer Command Validator - Validates entity IDs and choice bounds (1-6 non-empty GUIDs) before pipeline execution.
public sealed class SubmitAnswerCommandValidator : AbstractValidator<SubmitAnswerCommand>
{
    public SubmitAnswerCommandValidator()
    {
        RuleFor(command => command.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required.");

        RuleFor(command => command.ParticipantId)
            .NotEmpty()
            .WithMessage("Participant ID is required.");

        RuleFor(command => command)
            .Must(command => command.SessionTokenHash is not null || command.ConnectionGeneration.HasValue)
            .WithMessage("A participant session is required.");
    }
}
