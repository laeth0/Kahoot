using FluentValidation;

namespace Kahoot.Application.Games.SubmitAnswer;

public sealed class SubmitAnswerCommandValidator : AbstractValidator<SubmitAnswerCommand>
{
    public SubmitAnswerCommandValidator()
    {
        RuleFor(command => command.GameId).NotEmpty();
        RuleFor(command => command.QuestionId).NotEmpty();
        RuleFor(command => command.SelectedChoiceIds)
            .NotNull()
            .NotEmpty()
            .Must(ids => ids.Count <= 6 && ids.All(id => id != Guid.Empty));
        RuleFor(command => command.ParticipantSessionToken).NotEmpty().MaximumLength(512);
    }
}
