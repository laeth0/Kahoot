using Kahoot.Domain.Common;
using Kahoot.Domain.Hosts;
using Kahoot.Domain.Quizzes;

namespace Kahoot.Domain.Games;

public sealed class GameSession : AuditableEntity
{
    public Guid QuizId { get; set; }

    public Guid HostId { get; set; }

    public string Pin { get; set; } = null!;

    public GameStatus Status { get; set; }

    public Guid? CurrentQuestionId { get; set; }

    public int? CurrentQuestionIndex { get; set; }

    public DateTime? CurrentQuestionStartedAt { get; set; }

    public DateTime? CurrentQuestionEndsAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }

    public Quiz? Quiz { get; set; }

    public Host? Host { get; set; }

    public ICollection<Participant> Participants { get; set; } = [];

    public ICollection<Answer> Answers { get; set; } = [];
}
