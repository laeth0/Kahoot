using Kahoot.Domain.Common;
using Kahoot.Domain.Hosts;

namespace Kahoot.Domain.Quizzes;

public sealed class Quiz : AuditableEntity
{
    public Guid HostId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public Host? Host { get; set; }

    public ICollection<Question> Questions { get; set; } = [];
}
