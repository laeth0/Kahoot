using Kahoot.Domain.Common;

namespace Kahoot.Domain.Games;

public sealed class Participant : AuditableEntity
{
    private readonly List<Answer> _answers = [];

    private Participant()
    {
    }

    private Participant(Guid gameSessionId, string nickname, string sessionTokenHash, DateTime nowUtc)
    {
        GameSessionId = gameSessionId;
        Nickname = nickname;
        NicknameNormalized = Normalize(nickname);
        SessionTokenHash = sessionTokenHash;
        LastSeenAtUtc = nowUtc;
    }

    public Guid GameSessionId { get; private set; }

    public string Nickname { get; private set; } = null!;

    public string NicknameNormalized { get; private set; } = null!;

    public string SessionTokenHash { get; private set; } = null!;

    public string? ConnectionId { get; private set; }

    public DateTime LastSeenAtUtc { get; private set; }

    public int TotalScore { get; private set; }

    public int? LastRank { get; private set; }

    public bool IsRemoved { get; private set; }

    public DateTime? RemovedAtUtc { get; private set; }

    public GameSession? GameSession { get; private set; }

    public IReadOnlyCollection<Answer> Answers => _answers.AsReadOnly();

    public bool IsConnected => ConnectionId is not null;

    public static Participant Join(Guid gameSessionId, string nickname, string sessionTokenHash, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            throw new ArgumentException("Nickname is required.", nameof(nickname));
        }

        if (string.IsNullOrWhiteSpace(sessionTokenHash))
        {
            throw new ArgumentException("Session token hash is required.", nameof(sessionTokenHash));
        }

        return new Participant(gameSessionId, nickname.Trim(), sessionTokenHash, nowUtc);
    }

    public void MarkConnected(string connectionId, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            throw new ArgumentException("Connection id is required.", nameof(connectionId));
        }

        ConnectionId = connectionId;
        LastSeenAtUtc = nowUtc;
    }

    public void MarkDisconnected(DateTime nowUtc)
    {
        ConnectionId = null;
        LastSeenAtUtc = nowUtc;
    }

    public void AddScore(int points)
    {
        if (points < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "Awarded points cannot be negative.");
        }

        TotalScore += points;
    }

    public void SetRank(int rank)
    {
        if (rank < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), rank, "Rank is 1-based.");
        }

        LastRank = rank;
    }

    public void Remove(DateTime nowUtc)
    {
        IsRemoved = true;
        RemovedAtUtc = nowUtc;
        ConnectionId = null;
        LastSeenAtUtc = nowUtc;
    }

    private static string Normalize(string nickname) => nickname.Trim().ToLowerInvariant();
}
