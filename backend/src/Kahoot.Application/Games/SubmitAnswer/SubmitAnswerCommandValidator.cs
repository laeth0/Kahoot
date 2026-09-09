using FluentValidation;

namespace Kahoot.Application.Games.SubmitAnswer;

public sealed class SubmitAnswerCommandValidator : AbstractValidator<SubmitAnswerCommand>
{
    public SubmitAnswerCommandValidator()
    {
        RuleFor(command => command.GameId).NotEmpty();
        RuleFor(command => command.QuestionId).NotEmpty();
        RuleFor(command => command.SelectedChoiceId).NotEmpty();
        RuleFor(command => command.ParticipantSessionToken).NotEmpty().MaximumLength(512);
    }
}
