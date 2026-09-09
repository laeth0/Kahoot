using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Quizzes;

namespace Kahoot.Application.Quizzes.Questions.Common;

internal static class QuestionMapping
{
    public static List<Choice> BuildChoices(Guid questionId, IReadOnlyList<ChoiceInput> inputs) =>
        [.. inputs.Select((input, index) => new Choice
        {
            QuestionId = questionId,
            OrderIndex = index,
            Text = Normalize(input.Text),
            ImageUrl = Normalize(input.ImageUrl),
            IsCorrect = input.IsCorrect
        })];

    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
