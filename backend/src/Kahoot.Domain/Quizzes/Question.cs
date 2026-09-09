namespace Kahoot.Domain.Quizzes;

public sealed class Question
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid QuizId { get; set; }

    public int OrderIndex { get; set; }

    public string Text { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public int TimeLimitSeconds { get; set; }

    public int Points { get; set; }

    public Quiz? Quiz { get; set; }

    public ICollection<Choice> Choices { get; set; } = [];
}
