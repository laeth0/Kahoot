using Kahoot.Domain.Common;

namespace Kahoot.Domain.Quizzes;

public sealed class Choice : AuditableEntity
{
    private Choice()
    {
    }

    private Choice(Guid questionId, int orderIndex, string text, bool isCorrect)
    {
        QuestionId = questionId;
        OrderIndex = orderIndex;
        Text = text;
        IsCorrect = isCorrect;
    }

    public Guid QuestionId { get; private set; }

    public int OrderIndex { get; private set; }

    public string Text { get; private set; } = null!;

    public bool IsCorrect { get; private set; }

    public Question? Question { get; private set; }

    public static Choice Create(Guid questionId, int orderIndex, string text, bool isCorrect)
    {
        if (orderIndex is < 0 or >= Question.MaxChoices)
        {
            throw new ArgumentOutOfRangeException(nameof(orderIndex), orderIndex, "Choice order index is out of range.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Choice text is required.", nameof(text));
        }

        return new Choice(questionId, orderIndex, text.Trim(), isCorrect);
    }

    public void Update(string text, bool isCorrect)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Choice text is required.", nameof(text));
        }

        Text = text.Trim();
        IsCorrect = isCorrect;
    }
}
