using Kahoot.Domain.Enums;

namespace Kahoot.Domain.Entities;

public sealed class Game
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid SourceQuizId { get; set; }

    public required string Title { get; set; }

    public string? Pin { get; set; }

    public GameStatus Status { get; set; } = GameStatus.Created;

    public long StateVersion { get; set; } = 1;

    public long PresenceVersion { get; set; }

    public int ReservedParticipantCount { get; set; }

    public int NextSeatNumber { get; set; } = 1;

    public int? CurrentQuestionIndex { get; set; }

    public DateTimeOffset? HostGraceExpiresAt { get; set; }

    public bool IsTerminatedBySuspension { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
