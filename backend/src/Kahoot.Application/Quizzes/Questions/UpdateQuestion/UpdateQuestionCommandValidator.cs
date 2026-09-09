using FluentValidation;
using Kahoot.Application.Quizzes.Common;

namespace Kahoot.Application.Quizzes.Questions.UpdateQuestion;

public sealed class UpdateQuestionCommandValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionCommandValidator()
    {
        RuleFor(command => command.QuizId).NotEmpty();
        RuleFor(command => command.QuestionId).NotEmpty();
        RuleFor(command => command.Text).NotEmpty().MaximumLength(QuestionValidationRules.MaxQuestionTextLength);
        RuleFor(command => command.ImageUrl).MaximumLength(QuestionValidationRules.MaxImageUrlLength);
        RuleFor(command => command.TimeLimitSeconds)
            .InclusiveBetween(QuestionValidationRules.MinTimeLimitSeconds, QuestionValidationRules.MaxTimeLimitSeconds);
        RuleFor(command => command.Points).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Choices).ValidChoiceSet();
        RuleForEach(command => command.Choices).SetValidator(new ChoiceInputValidator());
    }
}
