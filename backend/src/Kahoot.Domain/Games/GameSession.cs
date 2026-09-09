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

    public DateTime? CurrentQuestionStartedAtUtc { get; set; }

    public DateTime? CurrentQuestionEndsAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? FinishedAtUtc { get; set; }

    public Quiz? Quiz { get; set; }

    public Host? Host { get; set; }

    public ICollection<Participant> Participants { get; set; } = [];

    public ICollection<Answer> Answers { get; set; } = [];
}
