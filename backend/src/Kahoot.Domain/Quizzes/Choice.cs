namespace Kahoot.Domain.Quizzes;

public sealed class Choice
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid QuestionId { get; set; }

    public int OrderIndex { get; set; }

    public string? Text { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsCorrect { get; set; }

    public Question? Question { get; set; }
}
