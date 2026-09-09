using Kahoot.Domain.Common;

namespace Kahoot.Domain.Games;

public sealed class Participant : AuditableEntity
{
    public Guid GameSessionId { get; set; }

    public string Nickname { get; set; } = null!;

    public string NicknameNormalized { get; set; } = null!;

    public string SessionTokenHash { get; set; } = null!;

    public string? ConnectionId { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public int TotalScore { get; set; }

    public int? LastRank { get; set; }

    public bool IsRemoved { get; set; }

    public DateTime? RemovedAtUtc { get; set; }

    public GameSession? GameSession { get; set; }

    public ICollection<Answer> Answers { get; set; } = [];
}
