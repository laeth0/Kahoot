using Kahoot.Domain.Hosts;

namespace Kahoot.Domain.Quizzes;

public sealed class Quiz
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HostId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsPublished { get; set; }

    public Host? Host { get; set; }

    public ICollection<Question> Questions { get; set; } = [];
}
